using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record NovoPagamento(int MesaId, int? ComandaId, MeioPagamento Meio, decimal ValorRecebido, int UsuarioId);

public record ResultadoPagamento(decimal ValorAbatido, decimal Troco, decimal SaldoRestanteMesa, bool MesaFinalizada);

public class PagamentoService(IDbContextFactory<AppDbContext> dbFactory, NotificadorMesas notificador, ProtecaoPin? protecao = null)
{
    /// <summary>
    /// Registra um pagamento parcial ou total. Sem comanda informada o valor abate o saldo da mesa inteira,
    /// distribuído entre as comandas abertas. Quando o saldo da mesa chega a zero ela é finalizada.
    /// </summary>
    public async Task<ResultadoPagamento> RegistrarAsync(NovoPagamento novo)
    {
        var valorRecebido = CalculoConta.Arredondar(novo.ValorRecebido);
        if (valorRecebido <= 0)
        {
            throw new RegraNegocioException("Informe um valor maior que zero.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        await UsuarioService.ValidarOperacaoAsync(db, novo.UsuarioId);
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        var turno = await db.TurnosCaixa.FirstOrDefaultAsync(t => t.DataHoraFechamento == null)
                    ?? throw new RegraNegocioException("Abra o caixa (turno) antes de receber pagamentos.");
        var mesa = await ContaMesa.ObterMesaAsync(db, novo.MesaId);
        var comandas = await ContaMesa.ComandasAbertasAsync(db, mesa.Id);

        var alvos = novo.ComandaId is { } comandaId
            ? comandas.Where(c => c.Id == comandaId).ToList()
            : comandas.OrderByDescending(c => c.ContaGeral).ThenBy(c => c.CodigoNumero).ToList();
        if (novo.ComandaId is not null && alvos.Count == 0)
        {
            throw new RegraNegocioException("A comanda selecionada não está aberta nesta mesa.");
        }

        var saldoAlvo = alvos.Sum(c => Math.Max(0, CalculoConta.Calcular(c, perfil).Saldo));
        if (saldoAlvo <= 0)
        {
            throw new RegraNegocioException("Não há saldo a receber.");
        }

        if (novo.Meio != MeioPagamento.Dinheiro && valorRecebido > saldoAlvo)
        {
            throw new RegraNegocioException(
                $"Em {TicketsImpressao.NomeMeio(novo.Meio)} o valor não pode passar do saldo ({TicketsImpressao.Moeda(saldoAlvo)}).");
        }

        var valorAbatido = Math.Min(valorRecebido, saldoAlvo);
        var troco = valorRecebido - valorAbatido;
        var restante = valorAbatido;
        Pagamento? ultimo = null;

        foreach (var comanda in alvos)
        {
            if (restante <= 0)
            {
                break;
            }

            var conta = CalculoConta.Calcular(comanda, perfil);
            var parte = Math.Min(restante, Math.Max(0, conta.Saldo));
            if (parte <= 0)
            {
                continue;
            }

            ultimo = new Pagamento
            {
                MesaId = mesa.Id,
                ComandaId = comanda.Id,
                TurnoCaixaId = turno.Id,
                UsuarioId = novo.UsuarioId,
                MeioPagamento = novo.Meio,
                ValorPago = parte,
                ValorTaxaServicoIncluso = CalculoConta.TaxaProporcional(parte, conta)
            };
            comanda.Pagamentos.Add(ultimo);
            restante -= parte;
        }

        ultimo!.ValorTroco = troco;

        // Comanda individual quitada é encerrada: o cliente pagou a parte dele e pode ir embora.
        foreach (var comanda in alvos.Where(c => !c.ContaGeral))
        {
            var conta = CalculoConta.Calcular(comanda, perfil);
            if (conta.Saldo <= 0 && conta.Total > 0)
            {
                ContaMesa.FecharComanda(db, comanda, mesa, novo.UsuarioId, perfil);
            }
        }

        var saldoMesa = CalculoConta.Calcular(comandas, perfil).Saldo;
        var finalizada = saldoMesa <= 0;
        if (finalizada)
        {
            MesaService.Finalizar(db, mesa, comandas, novo.UsuarioId, perfil);
            await db.SaveChangesAsync();
        }
        else
        {
            await ContaMesa.AtualizarValorAsync(db, mesa, perfil);
        }

        await transacao.CommitAsync();
        await notificador.NotificarAsync(mesa.Id);
        return new ResultadoPagamento(valorAbatido, troco, Math.Max(0, saldoMesa), finalizada);
    }

    /// <summary>Estorna um pagamento de conta ainda aberta. Exige PIN de gerente.</summary>
    public async Task EstornarAsync(int pagamentoId, string? pinGerente, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 3)
        {
            throw new RegraNegocioException("Informe o motivo do estorno.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var gerente = await UsuarioService.ValidarGerenteAsync(db, pinGerente, protecao);
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        var pagamento = await db.Pagamentos
            .Include(p => p.Comanda)
            .Include(p => p.TurnoCaixa)
            .Include(p => p.Mesa)
            .FirstOrDefaultAsync(p => p.Id == pagamentoId)
            ?? throw new RegraNegocioException("Pagamento não encontrado.");

        if (pagamento.Estornado)
        {
            throw new RegraNegocioException("Este pagamento já foi estornado.");
        }

        if (pagamento.Comanda is not { Status: StatusComanda.Aberta })
        {
            throw new RegraNegocioException("A conta deste pagamento já foi fechada.");
        }

        if (pagamento.TurnoCaixa is { Aberto: false })
        {
            throw new RegraNegocioException("O turno de caixa deste pagamento já foi fechado.");
        }

        pagamento.Estornado = true;
        pagamento.DataHoraEstorno = DateTime.Now;
        pagamento.MotivoEstorno = motivo.Trim()[..Math.Min(250, motivo.Trim().Length)];
        pagamento.UsuarioGerenteEstornoId = gerente.Id;
        await ContaMesa.AtualizarValorAsync(db, pagamento.Mesa, perfil);
        await notificador.NotificarAsync(pagamento.MesaId);
    }
}
