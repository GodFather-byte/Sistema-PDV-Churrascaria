using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class EstoqueServiceTests
{
    [Fact]
    public async Task BaixaEstoqueSomenteQuandoComandaEQuitada()
    {
        await using var ambiente = await CriarAsync();
        await HabilitarEstoqueAsync(ambiente, Chopp);
        await ambiente.Estoque.AjustarAsync(Chopp, 10m, true, "Compra inicial", Gerente);
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(Chopp, 2));
        var total = (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Total;

        await ambiente.PagarAsync(1, 10);

        Assert.Equal(10m, (await ambiente.Estoque.ListarProdutosAsync()).Single().QuantidadeEstoque);
        Assert.DoesNotContain(await ambiente.Estoque.ListarMovimentacoesAsync(),
            movimento => movimento.Tipo == TipoMovimentacaoEstoque.Venda);

        await ambiente.PagarAsync(1, total - 10);

        Assert.Equal(8m, (await ambiente.Estoque.ListarProdutosAsync()).Single().QuantidadeEstoque);
        var venda = Assert.Single((await ambiente.Estoque.ListarMovimentacoesAsync())
            .Where(movimento => movimento.Tipo == TipoMovimentacaoEstoque.Venda));
        Assert.Equal(-2m, venda.Quantidade);
        Assert.Equal(8m, venda.SaldoApos);
    }

    [Fact]
    public async Task CancelamentoRegistraPerdaEQuitacaoBaixaSomenteItensAtivos()
    {
        await using var ambiente = await CriarAsync();
        await HabilitarEstoqueAsync(ambiente, Chopp);
        await ambiente.Estoque.AjustarAsync(Chopp, 5m, true, "Compra inicial", Gerente);
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(Chopp, 3));
        var item = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single();

        await ambiente.Pedidos.CancelarItemAsync(item.Id, 1m, PinGerente, "Produto avariado");
        var total = (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Total;
        await ambiente.PagarAsync(1, total);

        Assert.Equal(2m, (await ambiente.Estoque.ListarProdutosAsync()).Single().QuantidadeEstoque);
        var movimentos = await ambiente.Estoque.ListarMovimentacoesAsync();
        Assert.Contains(movimentos, movimento =>
            movimento.Tipo == TipoMovimentacaoEstoque.Perda &&
            movimento.Quantidade == -1m &&
            movimento.Observacao!.Contains("Produto avariado"));
        Assert.Contains(movimentos, movimento =>
            movimento.Tipo == TipoMovimentacaoEstoque.Venda && movimento.Quantidade == -2m);
        await using var db = ambiente.NovoContexto();
        Assert.Equal(2, await db.MovimentacoesEstoque.CountAsync(movimento => movimento.ItemPedidoId != null));
    }

    [Fact]
    public async Task VendaPodeDeixarSaldoNegativo()
    {
        await using var ambiente = await CriarAsync();
        await HabilitarEstoqueAsync(ambiente, Chopp);
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(Chopp));
        var total = (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Total;

        await ambiente.PagarAsync(1, total);

        Assert.Equal(-1m, (await ambiente.Estoque.ListarProdutosAsync()).Single().QuantidadeEstoque);
    }

    [Fact]
    public async Task AjusteManualExigeGerenteEJustificativa()
    {
        await using var ambiente = await CriarAsync();
        await HabilitarEstoqueAsync(ambiente, Chopp);

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            ambiente.Estoque.AjustarAsync(Chopp, 1, true, "Compra", Garcom));
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            ambiente.Estoque.AjustarAsync(Chopp, 1, true, "", Gerente));
    }

    private static async Task HabilitarEstoqueAsync(AmbienteTeste ambiente, int produtoId)
    {
        await using var db = ambiente.NovoContexto();
        var produto = await db.Produtos.SingleAsync(p => p.Id == produtoId);
        produto.ControlaEstoque = true;
        await db.SaveChangesAsync();
    }
}
