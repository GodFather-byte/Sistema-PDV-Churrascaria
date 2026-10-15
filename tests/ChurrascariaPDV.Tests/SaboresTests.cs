using ChurrascariaPDV.Components.Shared;
using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class SaboresTests
{
    [Theory]
    [InlineData(RegraPrecoSabores.MaiorValor, 77)]
    [InlineData(RegraPrecoSabores.Media, 67)]
    [InlineData(RegraPrecoSabores.Soma, 127)]
    public async Task SaboresSubstituemBaseEAdicionaisSaoSomadosComHistorico(RegraPrecoSabores regra, decimal esperado)
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await cardapio.SalvarGrupoAsync(0, "Sabores", true, false, ["Calabresa", "Especial"],
            sabores: true, precosOpcoes: new Dictionary<string, decimal> { ["Calabresa"] = 50, ["Especial"] = 70 });
        await cardapio.SalvarGrupoAsync(0, "Borda", false, true, ["Recheada"],
            precosOpcoes: new Dictionary<string, decimal> { ["Recheada"] = 7 });
        var grupos = (await cardapio.ListarGruposAsync()).Where(g => g.Nome is "Sabores" or "Borda").ToList();
        await cardapio.SalvarProdutoAsync(new Produto
        {
            Nome = "Pizza grande", Preco = 999, CategoriaId = 3, SetorImpressaoId = SetorImpressao.Cozinha,
            RegraPrecoSabores = regra, MaximoSabores = 2
        }, grupos.Select(g => g.Id).ToList());
        var pizza = (await cardapio.ObterCardapioAsync()).SelectMany(c => c.Produtos).Single(p => p.Nome == "Pizza grande");
        var ids = grupos.SelectMany(g => g.Opcoes).Select(o => o.Id).ToArray();
        var carrinho = new ItemCarrinho { Produto = pizza, Quantidade = 2, OpcoesIds = ids };
        Assert.Equal(esperado * 2, carrinho.Total);
        await ambiente.LancarAsync(1, Item(pizza.Id, 2, ids));
        await using var db = ambiente.NovoContexto();
        var salvo = await db.ItensPedido.SingleAsync();
        Assert.Equal(esperado, salvo.PrecoUnitarioHistorico);
        Assert.Contains("1/2 Calabresa", salvo.ObservacoesModificadores);
        Assert.Contains("1/2 Especial", salvo.ObservacoesModificadores);
        var produto = await db.Produtos.FindAsync(pizza.Id);
        produto!.Preco = 1;
        foreach (var opcao in await db.OpcoesModificadores.Where(o => ids.Contains(o.Id)).ToListAsync()) opcao.Preco = 1;
        await db.SaveChangesAsync();
        await ambiente.Pedidos.ReimprimirPedidoAsync(salvo.PedidoId);
        Assert.Equal(esperado * 2, (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Subtotal);
    }

    [Fact]
    public void ValidaSaboresLimitesOpcoesEArredondamento()
    {
        var pizza = new ProdutoCardapio(1, "Pizza", 100, 1, "Pizzas", TipoItem.Produto, null,
            [new GrupoCardapio(1, "Sabores", true, false,
                [new(1, "A", 10), new(2, "B", 10), new(3, "C", 10.01m)], true)],
            RegraPrecoSabores.Media, 2);
        Assert.Throws<RegraNegocioException>(() => CalculoPrecoProduto.Calcular(pizza, []));
        Assert.Throws<RegraNegocioException>(() => CalculoPrecoProduto.Calcular(pizza, [1, 2, 3]));
        Assert.Throws<RegraNegocioException>(() => CalculoPrecoProduto.Calcular(pizza, [1, 999]));
        Assert.Equal(10, CalculoPrecoProduto.Calcular(pizza, [1]));
        Assert.Equal(10.01m, CalculoPrecoProduto.Calcular(pizza, [1, 3]));
    }

    [Fact]
    public async Task CadastroRejeitaDoisGruposSaboresEPrecoNegativo()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await Assert.ThrowsAsync<RegraNegocioException>(() => cardapio.SalvarGrupoAsync(0, "Inválido", true, false,
            ["A"], sabores: true, precosOpcoes: new Dictionary<string, decimal> { ["A"] = -1 }));
        await cardapio.SalvarGrupoAsync(0, "Sabores A", true, false, ["A"], sabores: true);
        await cardapio.SalvarGrupoAsync(0, "Sabores B", true, false, ["B"], sabores: true);
        var ids = (await cardapio.ListarGruposAsync()).Where(g => g.Sabores).Select(g => g.Id).ToList();
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            cardapio.SalvarProdutoAsync(new Produto { Nome = "Pizza", CategoriaId = 3 }, ids));
    }
}
