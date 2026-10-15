using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Licenciamento;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record TotalPorMeio(MeioPagamento Meio, int Quantidade, decimal Valor);

public record MovimentacaoResumo(DateTime DataHora, TipoMovimentacaoCaixa Tipo, decimal Valor, string? Observacao, string? Usuario);

public record ResumoTurno(
    int TurnoId,
    string Operador,
    DateTime Abertura,
    DateTime? Fechamento,
    decimal FundoTroco,
    IReadOnlyList<TotalPorMeio> PorMeio,
    decimal TotalRecebido,
    decimal TotalTaxaServico,
    decimal TotalDinheiro,
    decimal TotalSuprimentos,
    decimal TotalSangrias,
    decimal DinheiroEsperado,
    decimal? DinheiroInformado,
    int MesasFechadas,
    int ItensCancelados,
    IReadOnlyList<MovimentacaoResumo> Movimentacoes);

public record IndicadoresCaixa(int MesasOcupadas, int ContasSolicitadas, int TotalMesas, decimal TotalRecebidoTurno, bool TurnoAberto);

/// <summary>Turno de caixa: abertura com fundo de troco, sangria, suprimento e fechamento por forma de pagamento.</summary>
public class CaixaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ImpressaoService impressao,
    NotificadorMesas notificador,
    LicencaService? licenca = null)
{
    public async Task<TurnoCaixa?> ObterTurnoAbertoAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TurnosCaixa.AsNoTracking().Include(t => t.UsuarioAbertura).FirstOrDefaultAsync(t => t.DataHoraFechamento == null);
    }

    public async Task<IndicadoresCaixa> ObterIndicadoresAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var status = await db.Mesas.AsNoTracking().Select(m => m.Status).ToListAsync();
        var turno = await db.TurnosCaixa.AsNoTracking().FirstOrDefaultAsync(t => t.DataHoraFechamento == null);
        var total = 0m;
        if (turno is not null)
        {
            total = (await db.Pagamentos.AsNoTracking()
                    .Where(p => p.TurnoCaixaId == turno.Id && !p.Estornado)
                    .Select(p => p.ValorPago)
                    .ToListAsync())
                .Sum();
        }

        return new IndicadoresCaixa(
            status.Count(s => s is StatusMesa.Ocupada or StatusMesa.ContaSolicitada),
            status.Count(s => s == StatusMesa.ContaSolicitada),
            status.Count,
            total,
            turno is not null);
    }

    public async Task AbrirTurnoAsync(decimal fundoTroco, int usuarioId)
    {
        if (licenca is not null) await licenca.GarantirPodeVenderAsync();
        if (fundoTroco < 0)
        {
            throw new RegraNegocioException("O fundo de troco não pode ser negativo.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.TurnosCaixa.AnyAsync(t => t.DataHoraFechamento == null))
        {
            throw new RegraNegocioException("Já existe um turno de caixa aberto.");
        }

        await UsuarioService.ValidarOperacaoAsync(db, usuarioId);
        db.TurnosCaixa.Add(new TurnoCaixa
        {
            FundoTroco = CalculoConta.Arredondar(fundoTroco),
            UsuarioAberturaId = usuarioId,
            DataHoraAbertura = DateTime.Now
        });
        await db.SaveChangesAsync();
        await notificador.NotificarAsync();
    }

    public async Task RegistrarMovimentacaoAsync(TipoMovimentacaoCaixa tipo, decimal valor, string? observacao, int usuarioId)
    {
        valor = CalculoConta.Arredondar(valor);
        if (valor <= 0)
        {
            throw new RegraNegocioException("Informe um valor maior que zero.");
        }

        if (tipo == TipoMovimentacaoCaixa.Sangria && string.IsNullOrWhiteSpace(observacao))
        {
            throw new RegraNegocioException("Informe o motivo/destino da sangria.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var turno = await db.TurnosCaixa.FirstOrDefaultAsync(t => t.DataHoraFechamento == null)
                    ?? throw new RegraNegocioException("Não há turno de caixa aberto.");
        await UsuarioService.ValidarOperacaoAsync(db, usuarioId);

        if (tipo == TipoMovimentacaoCaixa.Sangria)
        {
            var resumo = await CalcularResumoAsync(db, turno);
            if (valor > resumo.DinheiroEsperado)
            {
                throw new RegraNegocioException($"Sangria maior que o dinheiro em caixa ({TicketsImpressao.Moeda(resumo.DinheiroEsperado)}).");
            }
        }

        db.MovimentacoesCaixa.Add(new MovimentacaoCaixa
        {
            TurnoCaixaId = turno.Id,
            Tipo = tipo,
            Valor = valor,
            Observacao = observacao?.Trim(),
            UsuarioId = usuarioId
        });
        await db.SaveChangesAsync();
        await notificador.NotificarAsync();
    }

    public async Task<ResumoTurno?> ObterResumoAsync(int? turnoId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var turno = turnoId is null
            ? await db.TurnosCaixa.Include(t => t.UsuarioAbertura).FirstOrDefaultAsync(t => t.DataHoraFechamento == null)
            : await db.TurnosCaixa.Include(t => t.UsuarioAbertura).FirstOrDefaultAsync(t => t.Id == turnoId);
        return turno is null ? null : await CalcularResumoAsync(db, turno);
    }

    public async Task<ResumoTurno> FecharTurnoAsync(decimal dinheiroContado, string? observacao, int usuarioId)
    {
        if (dinheiroContado < 0)
        {
            throw new RegraNegocioException("O valor contado não pode ser negativo.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        await UsuarioService.ValidarOperacaoAsync(db, usuarioId);
        var turno = await db.TurnosCaixa.Include(t => t.UsuarioAbertura).FirstOrDefaultAsync(t => t.DataHoraFechamento == null)
                    ?? throw new RegraNegocioException("Não há turno de caixa aberto.");

        var resumo = await CalcularResumoAsync(db, turno);
        turno.DataHoraFechamento = DateTime.Now;
        turno.UsuarioFechamentoId = usuarioId;
        turno.ValorFechamento = CalculoConta.Arredondar(dinheiroContado);
        turno.ValorEsperadoDinheiro = resumo.DinheiroEsperado;
        turno.Observacao = observacao?.Trim();
        await db.SaveChangesAsync();

        resumo = resumo with { Fechamento = turno.DataHoraFechamento, DinheiroInformado = turno.ValorFechamento };
        var documento = TicketsImpressao.FechamentoTurno(
            impressao.NovoDocumento(perfil), perfil.Nome, resumo, perfil.PercentualTaxaServico);
        var setor = await SetorImpressaoService.ObterSetorContasAsync(db);
        await impressao.ImprimirAsync(setor, $"Fechamento do turno #{turno.Id}", documento);
        await notificador.NotificarAsync();
        return resumo;
    }

    public async Task<List<TurnoCaixa>> ListarTurnosAsync(int quantidade = 30)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TurnosCaixa.AsNoTracking()
            .Include(t => t.UsuarioAbertura)
            .OrderByDescending(t => t.DataHoraAbertura)
            .Take(quantidade)
            .ToListAsync();
    }

    public async Task<bool> ReimprimirFechamentoAsync(int turnoId)
    {
        var resumo = await ObterResumoAsync(turnoId) ?? throw new RegraNegocioException("Turno não encontrado.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        var documento = TicketsImpressao.FechamentoTurno(
            impressao.NovoDocumento(perfil), perfil.Nome, resumo, perfil.PercentualTaxaServico);
        var setor = await SetorImpressaoService.ObterSetorContasAsync(db);
        return await impressao.ImprimirAsync(setor, $"Fechamento do turno #{turnoId} (2ª via)", documento);
    }

    private static async Task<ResumoTurno> CalcularResumoAsync(AppDbContext db, TurnoCaixa turno)
    {
        var fim = turno.DataHoraFechamento ?? DateTime.Now;
        var pagamentos = await db.Pagamentos.AsNoTracking()
            .Where(p => p.TurnoCaixaId == turno.Id && !p.Estornado)
            .ToListAsync();
        var movimentacoes = await db.MovimentacoesCaixa.AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.TurnoCaixaId == turno.Id)
            .OrderBy(m => m.DataHora)
            .ToListAsync();
        var fechamentos = await db.FechamentosConta.AsNoTracking()
            .CountAsync(f => f.DataHora >= turno.DataHoraAbertura && f.DataHora <= fim);
        var cancelados = await db.ItensPedido.AsNoTracking()
            .CountAsync(i => i.Cancelado && i.DataHoraCancelamento >= turno.DataHoraAbertura && i.DataHoraCancelamento <= fim);

        var porMeio = Enum.GetValues<MeioPagamento>()
            .Select(meio =>
            {
                var doMeio = pagamentos.Where(p => p.MeioPagamento == meio).ToList();
                return new TotalPorMeio(meio, doMeio.Count, doMeio.Sum(p => p.ValorPago));
            })
            .ToList();

        var dinheiro = porMeio.First(m => m.Meio == MeioPagamento.Dinheiro).Valor;
        var suprimentos = movimentacoes.Where(m => m.Tipo == TipoMovimentacaoCaixa.Suprimento).Sum(m => m.Valor);
        var sangrias = movimentacoes.Where(m => m.Tipo == TipoMovimentacaoCaixa.Sangria).Sum(m => m.Valor);

        return new ResumoTurno(
            turno.Id,
            turno.UsuarioAbertura?.Nome ?? "-",
            turno.DataHoraAbertura,
            turno.DataHoraFechamento,
            turno.FundoTroco,
            porMeio,
            pagamentos.Sum(p => p.ValorPago),
            pagamentos.Sum(p => p.ValorTaxaServicoIncluso),
            dinheiro,
            suprimentos,
            sangrias,
            turno.FundoTroco + dinheiro + suprimentos - sangrias,
            turno.ValorFechamento,
            fechamentos,
            cancelados,
            movimentacoes.Select(m => new MovimentacaoResumo(m.DataHora, m.Tipo, m.Valor, m.Observacao, m.Usuario?.Nome)).ToList());
    }
}
