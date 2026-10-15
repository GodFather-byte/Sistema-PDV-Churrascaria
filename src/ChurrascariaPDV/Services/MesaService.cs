using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record MesaResumo(
    int Id,
    int Numero,
    StatusMesa Status,
    DateTime? DataHoraAbertura,
    int QuantidadePessoas,
    int CapacidadeLugares,
    decimal Valor,
    int QuantidadeComandas);

public record ItemConsumo(
    int Id,
    int PedidoId,
    int ComandaId,
    int ProdutoId,
    string Produto,
    SetorImpressao Setor,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal Total,
    string? Observacoes,
    string Garcom,
    DateTime DataHora,
    bool Cancelado,
    string? MotivoCancelamento,
    bool Impresso);

public record PagamentoRealizado(
    int Id,
    int? ComandaId,
    DateTime DataHora,
    MeioPagamento Meio,
    decimal ValorPago,
    decimal ValorTroco);

public record ComandaDetalhe(
    int Id,
    int CodigoNumero,
    string Descricao,
    string? NomeCliente,
    bool ContaGeral,
    bool CobrarTaxaServico,
    ResumoConta Resumo,
    IReadOnlyList<ItemConsumo> Itens,
    IReadOnlyList<PagamentoRealizado> Pagamentos);

public record MesaDetalhe(
    int Id,
    int Numero,
    StatusMesa Status,
    DateTime? DataHoraAbertura,
    int QuantidadePessoas,
    int CapacidadeLugares,
    IReadOnlyList<ComandaDetalhe> Comandas,
    ResumoConta Resumo)
{
    public IEnumerable<ItemConsumo> Itens => Comandas.SelectMany(c => c.Itens).OrderBy(i => i.DataHora).ThenBy(i => i.Id);
    public bool TemConsumo => Comandas.Any(c => c.Itens.Any(i => !i.Cancelado));
    public int PessoasParaDivisao => Math.Max(1, QuantidadePessoas);
}

public class MesaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ImpressaoService impressao,
    NotificadorMesas notificador)
{
    public async Task<List<MesaResumo>> ListarMapaAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesas = await db.Mesas.AsNoTracking().OrderBy(m => m.Numero).ToListAsync();
        var comandasPorMesa = await db.Comandas.AsNoTracking()
            .Where(c => c.Status == StatusComanda.Aberta && c.CodigoNumero != Comanda.CodigoContaGeral)
            .GroupBy(c => c.MesaId)
            .Select(g => new { MesaId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.MesaId, g => g.Quantidade);

        return mesas.Select(m => new MesaResumo(
                m.Id, m.Numero, m.Status, m.DataHoraAbertura, m.QuantidadePessoas, m.CapacidadeLugares,
                m.ValorTotalAtual, comandasPorMesa.GetValueOrDefault(m.Id)))
            .ToList();
    }

    public async Task<MesaDetalhe> ObterDetalheAsync(int mesaId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await db.Mesas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mesaId)
                   ?? throw new RegraNegocioException("Mesa não encontrada.");
        var comandas = await ContaMesa.ComandasCompletas(db).AsNoTracking()
            .Where(c => c.MesaId == mesaId && c.Status == StatusComanda.Aberta)
            .OrderBy(c => c.CodigoNumero)
            .ToListAsync();

        var detalhes = comandas.Select(MapearComanda).ToList();
        return new MesaDetalhe(mesa.Id, mesa.Numero, mesa.Status, mesa.DataHoraAbertura, mesa.QuantidadePessoas,
            mesa.CapacidadeLugares, detalhes, CalculoConta.Calcular(comandas));
    }

    public async Task<int?> ObterIdPorNumeroAsync(int numero)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Mesas.Where(m => m.Numero == numero).Select(m => (int?)m.Id).FirstOrDefaultAsync();
    }

    public async Task AbrirMesaAsync(int mesaId, int pessoas)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        if (mesa.Status is StatusMesa.Ocupada or StatusMesa.ContaSolicitada)
        {
            throw new RegraNegocioException($"A mesa {mesa.Numero} já está aberta.");
        }

        ContaMesa.Ocupar(mesa, Math.Max(1, pessoas));
        await ContaMesa.ObterOuCriarContaGeralAsync(db, mesa);
        await db.SaveChangesAsync();
        await notificador.NotificarAsync(mesa.Id);
    }

    public async Task DefinirPessoasAsync(int mesaId, int pessoas)
    {
        if (pessoas is < 1 or > 99)
        {
            throw new RegraNegocioException("Informe entre 1 e 99 pessoas.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        if (mesa.Status is StatusMesa.Livre or StatusMesa.AguardandoLimpeza)
        {
            throw new RegraNegocioException("Abra a mesa antes de informar as pessoas.");
        }

        mesa.QuantidadePessoas = pessoas;
        await db.SaveChangesAsync();
        await notificador.NotificarAsync(mesa.Id);
    }

    /// <summary>Cria uma comanda individual (cartão) vinculada à mesa, abrindo a mesa se necessário.</summary>
    public async Task<int> CriarComandaAsync(int mesaId, int? codigo, string? nomeCliente)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);

        var codigosEmUso = await db.Comandas
            .Where(c => c.Status == StatusComanda.Aberta && c.CodigoNumero != Comanda.CodigoContaGeral)
            .Select(c => c.CodigoNumero)
            .ToListAsync();

        var numero = codigo ?? Math.Max(100, codigosEmUso.DefaultIfEmpty(100).Max()) + 1;
        if (numero <= 0)
        {
            throw new RegraNegocioException("O número da comanda deve ser maior que zero.");
        }

        if (codigosEmUso.Contains(numero))
        {
            throw new RegraNegocioException($"A comanda {numero} já está em uso em outra mesa.");
        }

        ContaMesa.Ocupar(mesa, 1);
        var comanda = new Comanda
        {
            MesaId = mesa.Id,
            CodigoNumero = numero,
            NomeCliente = string.IsNullOrWhiteSpace(nomeCliente) ? null : nomeCliente.Trim()[..Math.Min(100, nomeCliente.Trim().Length)]
        };
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        await notificador.NotificarAsync(mesa.Id);
        return comanda.Id;
    }

    /// <summary>Liga/desliga os 10% de uma comanda ou de todas as comandas abertas da mesa.</summary>
    public async Task DefinirTaxaServicoAsync(int mesaId, int? comandaId, bool cobrar)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        var comandas = (await ContaMesa.ComandasAbertasAsync(db, mesaId))
            .Where(c => comandaId is null || c.Id == comandaId)
            .ToList();
        if (comandas.Count == 0)
        {
            throw new RegraNegocioException("Não há comanda aberta para alterar a taxa de serviço.");
        }

        foreach (var comanda in comandas)
        {
            comanda.CobrarTaxaServico = cobrar;
            ContaMesa.GarantirSaldoNaoNegativo(comanda);
        }

        await ContaMesa.AtualizarValorAsync(db, mesa);
        await notificador.NotificarAsync(mesa.Id);
    }

    /// <summary>Garçom ou caixa pede a pré-conta: mesa fica amarela e a conferência é impressa no caixa.</summary>
    public async Task<bool> SolicitarContaAsync(int mesaId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        var comandas = await ContaMesa.ComandasAbertasAsync(db, mesaId);
        if (mesa.Status is StatusMesa.Livre or StatusMesa.AguardandoLimpeza || !comandas.Any(c => c.Pedidos.SelectMany(p => p.Itens).Any(i => !i.Cancelado)))
        {
            throw new RegraNegocioException($"A mesa {mesa.Numero} não tem consumo para fechar.");
        }

        mesa.Status = StatusMesa.ContaSolicitada;
        await ContaMesa.AtualizarValorAsync(db, mesa);
        await notificador.NotificarAsync(mesa.Id);

        return await ImprimirPreContaAsync(mesa, comandas);
    }

    /// <summary>Reimprime a conferência sem alterar o status da mesa.</summary>
    public async Task<bool> ReimprimirPreContaAsync(int mesaId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        var comandas = await ContaMesa.ComandasAbertasAsync(db, mesaId);
        if (comandas.Count == 0)
        {
            throw new RegraNegocioException($"A mesa {mesa.Numero} não tem conta aberta.");
        }

        return await ImprimirPreContaAsync(mesa, comandas);
    }

    /// <summary>Move os clientes (todas as comandas abertas) de uma mesa para outra, juntando as contas se o destino estiver ocupado.</summary>
    public async Task TransferirMesaAsync(int mesaOrigemId, int mesaDestinoId)
    {
        if (mesaOrigemId == mesaDestinoId)
        {
            throw new RegraNegocioException("Escolha uma mesa de destino diferente da origem.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        var origem = await ContaMesa.ObterMesaAsync(db, mesaOrigemId);
        var destino = await ContaMesa.ObterMesaAsync(db, mesaDestinoId);
        var comandasOrigem = await ContaMesa.ComandasAbertasAsync(db, origem.Id);
        if (comandasOrigem.Count == 0)
        {
            throw new RegraNegocioException($"A mesa {origem.Numero} não tem conta aberta para transferir.");
        }

        var destinoEstavaAberto = destino.Status is StatusMesa.Ocupada or StatusMesa.ContaSolicitada;
        var contaGeralDestino = destinoEstavaAberto
            ? (await ContaMesa.ComandasAbertasAsync(db, destino.Id)).FirstOrDefault(c => c.ContaGeral)
            : null;

        foreach (var comanda in comandasOrigem)
        {
            if (comanda.ContaGeral && contaGeralDestino is not null)
            {
                foreach (var pedido in comanda.Pedidos.ToList())
                {
                    pedido.ComandaId = contaGeralDestino.Id;
                    pedido.MesaId = destino.Id;
                }

                foreach (var pagamento in comanda.Pagamentos.ToList())
                {
                    pagamento.ComandaId = contaGeralDestino.Id;
                    pagamento.MesaId = destino.Id;
                }

                comanda.Status = StatusComanda.Cancelada;
                comanda.DataHoraFechamento = DateTime.Now;
                continue;
            }

            comanda.MesaId = destino.Id;
            foreach (var pedido in comanda.Pedidos)
            {
                pedido.MesaId = destino.Id;
            }

            foreach (var pagamento in comanda.Pagamentos)
            {
                pagamento.MesaId = destino.Id;
            }
        }

        if (destinoEstavaAberto)
        {
            destino.QuantidadePessoas += origem.QuantidadePessoas;
            if (origem.DataHoraAbertura < destino.DataHoraAbertura)
            {
                destino.DataHoraAbertura = origem.DataHoraAbertura;
            }

            if (origem.Status == StatusMesa.Ocupada)
            {
                destino.Status = StatusMesa.Ocupada;
            }
        }
        else
        {
            destino.Status = origem.Status;
            destino.DataHoraAbertura = origem.DataHoraAbertura;
            destino.QuantidadePessoas = origem.QuantidadePessoas;
        }

        ContaMesa.Desocupar(origem, StatusMesa.AguardandoLimpeza);
        await ContaMesa.AtualizarValorAsync(db, destino);
        await transacao.CommitAsync();
        await notificador.NotificarAsync(origem.Id, destino.Id);
    }

    /// <summary>Fecha todas as comandas quitadas e libera a mesa para limpeza.</summary>
    public async Task FinalizarMesaAsync(int mesaId, int? usuarioId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        var comandas = await ContaMesa.ComandasAbertasAsync(db, mesaId);
        var resumo = CalculoConta.Calcular(comandas);
        if (resumo.Saldo > 0)
        {
            throw new RegraNegocioException($"A mesa {mesa.Numero} ainda deve {TicketsImpressao.Moeda(resumo.Saldo)}.");
        }

        Finalizar(db, mesa, comandas, usuarioId);
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
        await notificador.NotificarAsync(mesa.Id);
    }

    internal static void Finalizar(AppDbContext db, Mesa mesa, IEnumerable<Comanda> comandas, int? usuarioId)
    {
        var houveConsumo = false;
        foreach (var comanda in comandas)
        {
            houveConsumo |= comanda.Pedidos.SelectMany(p => p.Itens).Any() || comanda.Pagamentos.Any(p => !p.Estornado);
            if (comanda.Status == StatusComanda.Aberta)
            {
                ContaMesa.FecharComanda(db, comanda, mesa, usuarioId);
            }
        }

        ContaMesa.Desocupar(mesa, houveConsumo ? StatusMesa.AguardandoLimpeza : StatusMesa.Livre);
    }

    public async Task LiberarMesaAsync(int mesaId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mesa = await ContaMesa.ObterMesaAsync(db, mesaId);
        if (mesa.Status != StatusMesa.AguardandoLimpeza)
        {
            throw new RegraNegocioException($"A mesa {mesa.Numero} não está aguardando limpeza.");
        }

        ContaMesa.Desocupar(mesa, StatusMesa.Livre);
        await db.SaveChangesAsync();
        await notificador.NotificarAsync(mesa.Id);
    }

    private async Task<bool> ImprimirPreContaAsync(Mesa mesa, IReadOnlyList<Comanda> comandas)
    {
        var ticket = comandas
            .Select(c => new ComandaTicketConta(
                c.Descricao,
                c.Pedidos.SelectMany(p => p.Itens)
                    .Where(i => !i.Cancelado)
                    .GroupBy(i => new { i.ProdutoId, i.Produto.Nome, i.PrecoUnitarioHistorico })
                    .Select(g => new LinhaTicketConta(g.Sum(i => i.Quantidade), g.Key.Nome, g.Key.PrecoUnitarioHistorico,
                        g.Sum(i => i.ValorTotal), null))
                    .ToList(),
                CalculoConta.Calcular(c)))
            .Where(c => c.Itens.Count > 0 || c.Resumo.Pago > 0)
            .ToList();

        var documento = TicketsImpressao.PreConta(impressao.NovoDocumento(), impressao.Configuracao.NomeEstabelecimento,
            mesa.Numero, mesa.DataHoraAbertura, mesa.QuantidadePessoas, ticket);
        return await impressao.ImprimirAsync(SetorImpressao.Caixa, $"Pré-conta mesa {mesa.Numero}", documento);
    }

    private static ComandaDetalhe MapearComanda(Comanda comanda) => new(
        comanda.Id,
        comanda.CodigoNumero,
        comanda.Descricao,
        comanda.NomeCliente,
        comanda.ContaGeral,
        comanda.CobrarTaxaServico,
        CalculoConta.Calcular(comanda),
        comanda.Pedidos
            .SelectMany(p => p.Itens.Select(i => new ItemConsumo(
                i.Id, p.Id, comanda.Id, i.ProdutoId, i.Produto.Nome, i.Produto.SetorImpressao, i.Quantidade,
                i.PrecoUnitarioHistorico, CalculoConta.Arredondar(i.Quantidade * i.PrecoUnitarioHistorico),
                i.ObservacoesModificadores, p.UsuarioGarcom.Nome, p.DataHoraLancamento, i.Cancelado,
                i.MotivoCancelamento, p.ImpressoNosSetores)))
            .OrderBy(i => i.DataHora).ThenBy(i => i.Id)
            .ToList(),
        comanda.Pagamentos
            .Where(p => !p.Estornado)
            .OrderBy(p => p.DataHora)
            .Select(p => new PagamentoRealizado(p.Id, p.ComandaId, p.DataHora, p.MeioPagamento, p.ValorPago, p.ValorTroco))
            .ToList());
}
