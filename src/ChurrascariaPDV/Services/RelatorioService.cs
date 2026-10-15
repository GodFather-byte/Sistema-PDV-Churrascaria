using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record VendaProduto(string Produto, string Categoria, decimal Quantidade, decimal Valor);

public record VendaGarcom(string Garcom, int Pedidos, decimal ValorItens, decimal TaxaServicoEstimada);

public record CancelamentoRelatorio(DateTime DataHora, int Mesa, string Produto, decimal Quantidade, decimal Valor, string? Motivo, string? Gerente, string Garcom);

public record EstornoRelatorio(DateTime DataHora, int Mesa, MeioPagamento Meio, decimal Valor, string? Motivo, string? Gerente, string? Operador);

public record RelatorioVendas(
    DateTime Inicio,
    DateTime Fim,
    decimal PercentualTaxaServico,
    IReadOnlyList<TotalPorMeio> PorMeio,
    decimal TotalRecebido,
    decimal TotalTaxaServico,
    int ContasFechadas,
    decimal TicketMedio,
    IReadOnlyList<VendaProduto> Produtos,
    IReadOnlyList<VendaGarcom> Garcons,
    IReadOnlyList<CancelamentoRelatorio> Cancelamentos,
    IReadOnlyList<EstornoRelatorio> Estornos);

public class RelatorioService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<RelatorioVendas> GerarAsync(DateTime inicio, DateTime fim)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);

        var pagamentos = await db.Pagamentos.AsNoTracking()
            .Where(p => !p.Estornado && p.DataHora >= inicio && p.DataHora < fim)
            .ToListAsync();

        var itens = await db.ItensPedido.AsNoTracking()
            .Include(i => i.Produto).ThenInclude(p => p.Categoria)
            .Include(i => i.Pedido).ThenInclude(p => p.UsuarioGarcom)
            .Include(i => i.Pedido).ThenInclude(p => p.Comanda)
            .Include(i => i.Pedido).ThenInclude(p => p.Mesa)
            .Include(i => i.UsuarioGerenteAutorizacao)
            .Where(i => i.Pedido.DataHoraLancamento >= inicio && i.Pedido.DataHoraLancamento < fim)
            .AsSplitQuery()
            .ToListAsync();

        var fechamentos = await db.FechamentosConta.AsNoTracking()
            .Where(f => f.DataHora >= inicio && f.DataHora < fim)
            .ToListAsync();

        var porMeio = Enum.GetValues<MeioPagamento>()
            .Select(meio =>
            {
                var doMeio = pagamentos.Where(p => p.MeioPagamento == meio).ToList();
                return new TotalPorMeio(meio, doMeio.Count, doMeio.Sum(p => p.ValorPago));
            })
            .ToList();

        var validos = itens.Where(i => !i.Cancelado).ToList();
        var produtos = validos
            .GroupBy(i => new { i.Produto.Nome, Categoria = i.Produto.Categoria.Nome })
            .Select(g => new VendaProduto(g.Key.Nome, g.Key.Categoria, g.Sum(i => i.Quantidade), g.Sum(i => i.ValorTotal)))
            .OrderByDescending(v => v.Valor)
            .ToList();

        var garcons = validos
            .GroupBy(i => i.Pedido.UsuarioGarcom.Nome)
            .Select(g => new VendaGarcom(
                g.Key,
                g.Select(i => i.PedidoId).Distinct().Count(),
                g.Sum(i => i.ValorTotal),
                g.Sum(i => CalculoConta.CalcularTaxa(
                    i.ValorTotal, i.Pedido.Comanda.CobrarTaxaServico, perfil))))
            .OrderByDescending(v => v.ValorItens)
            .ToList();

        var cancelamentos = itens
            .Where(i => i.Cancelado)
            .OrderByDescending(i => i.DataHoraCancelamento)
            .Select(i => new CancelamentoRelatorio(
                i.DataHoraCancelamento ?? i.Pedido.DataHoraLancamento,
                i.Pedido.Mesa.Numero,
                i.Produto.Nome,
                i.Quantidade,
                CalculoConta.Arredondar(i.Quantidade * i.PrecoUnitarioHistorico),
                i.MotivoCancelamento,
                i.UsuarioGerenteAutorizacao?.Nome,
                i.Pedido.UsuarioGarcom.Nome))
            .ToList();

        var estornos = (await db.Pagamentos.AsNoTracking()
                .Include(p => p.Mesa)
                .Include(p => p.Usuario)
                .Include(p => p.UsuarioGerenteEstorno)
                .Where(p => p.Estornado && p.DataHoraEstorno >= inicio && p.DataHoraEstorno < fim)
                .ToListAsync())
            .OrderByDescending(p => p.DataHoraEstorno)
            .Select(p => new EstornoRelatorio(p.DataHoraEstorno!.Value, p.Mesa.Numero, p.MeioPagamento, p.ValorPago,
                p.MotivoEstorno, p.UsuarioGerenteEstorno?.Nome, p.Usuario?.Nome))
            .ToList();

        var totalRecebido = pagamentos.Sum(p => p.ValorPago);
        return new RelatorioVendas(
            inicio,
            fim,
            perfil.PercentualTaxaServico,
            porMeio,
            totalRecebido,
            pagamentos.Sum(p => p.ValorTaxaServicoIncluso),
            fechamentos.Count,
            fechamentos.Count == 0 ? 0 : CalculoConta.Arredondar(fechamentos.Sum(f => f.Total) / fechamentos.Count),
            produtos,
            garcons,
            cancelamentos,
            estornos);
    }
}
