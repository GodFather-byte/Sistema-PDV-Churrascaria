using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class RegrasModificadoresTests
{
    [Theory]
    [InlineData(TipoEstabelecimento.Bar)]
    [InlineData(TipoEstabelecimento.Pizzaria)]
    public async Task PerfilSemRodizioOuPontoDeCarnePodeVenderProdutoComum(TipoEstabelecimento tipo)
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await using (var db = ambiente.NovoContexto())
        {
            var perfil = await db.PerfisEstabelecimento.SingleAsync();
            perfil.Tipo = tipo;
            db.GruposModificadoresProdutos.RemoveRange(await db.GruposModificadoresProdutos.ToListAsync());
            foreach (var produto in await db.Produtos.ToListAsync()) produto.Ativo = false;
            await db.SaveChangesAsync();
        }
        await cardapio.SalvarCategoriaAsync(0, "Cardápio próprio");
        var categoria = (await cardapio.ListarCategoriasAsync()).Single(c => c.Nome == "Cardápio próprio");
        await cardapio.SalvarProdutoAsync(
            new Produto { Nome = "Produto comum", Preco = 25m, CategoriaId = categoria.Id, SetorImpressaoId = SetorImpressao.Cozinha }, []);
        var comum = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Produto comum");

        var resultado = await ambiente.LancarAsync(1, Item(comum.Id));
        var menu = await cardapio.ObterCardapioAsync();

        Assert.Equal(TipoItem.Produto, comum.TipoItem);
        Assert.False(Assert.Single(Assert.Single(menu).Produtos).TemModificadores);
        Assert.True(resultado.ImpressoComSucesso);
        Assert.Equal(25m, (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Subtotal);
    }

    [Fact]
    public async Task GrupoDePizzaValidaEscolhaUnicaEEnviaParaForno()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        var forno = new SetorImpressao { Nome = "Forno", Ip = "127.0.0.1", ObservacoesEmDestaque = false };
        await ambiente.Servico<SetorImpressaoService>().SalvarAsync(forno);
        await cardapio.SalvarGrupoAsync(0, "Borda", true, true, ["Recheada", "Tradicional"], forno.Id, destacarObservacoes: true);
        var grupo = (await cardapio.ListarGruposAsync()).Single(g => g.Nome == "Borda");
        await cardapio.SalvarProdutoAsync(
            new Produto { Nome = "Pizza", Preco = 50m, CategoriaId = 3 }, [grupo.Id]);
        var pizza = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Pizza");
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.LancarAsync(1, Item(pizza.Id)));
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.LancarAsync(1, Item(pizza.Id, 1, grupo.Opcoes.Select(o => o.Id).ToArray())));

        var resultado = await ambiente.LancarAsync(1, Item(pizza.Id, 1, grupo.Opcoes.First().Id));

        Assert.Equal(forno.Id, Assert.Single(resultado.SetoresImpressos).Id);
        Assert.Contains("RECHEADA", Assert.Single(ambiente.Impressoes.Listar()).Conteudo);
        Assert.Equal(forno.Id, (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single().Setor?.Id);
    }

    [Fact]
    public async Task GruposComSetoresConflitantesSaoRejeitadosSemSalvarPedido()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await cardapio.SalvarGrupoAsync(0, "Preparo A", false, true, ["A"], SetorImpressao.Cozinha);
        await cardapio.SalvarGrupoAsync(0, "Preparo B", false, true, ["B"], SetorImpressao.BarCopa);
        var grupos = (await cardapio.ListarGruposAsync()).Where(g => g.Nome.StartsWith("Preparo")).ToList();
        await cardapio.SalvarProdutoAsync(new Produto { Nome = "Combinado", Preco = 20m, CategoriaId = 3 },
            grupos.Select(g => g.Id).ToList());
        var produto = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Combinado");

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            ambiente.LancarAsync(1, Item(produto.Id, 1, grupos.SelectMany(g => g.Opcoes).Select(o => o.Id).ToArray())));

        await using var db = ambiente.NovoContexto();
        Assert.Empty(await db.Pedidos.ToListAsync());
        Assert.Equal(StatusMesa.Livre, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
    }

    [Fact]
    public async Task ReimpressaoTransferenciaECancelamentoPreservamSetorOriginal()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await cardapio.SalvarGrupoAsync(0, "Preparo", true, true, ["Assado"], SetorImpressao.Cozinha, destacarObservacoes: true);
        var grupo = (await cardapio.ListarGruposAsync()).Single(g => g.Nome == "Preparo");
        await cardapio.SalvarProdutoAsync(new Produto { Nome = "Lanche", Preco = 30m, CategoriaId = 3 }, [grupo.Id]);
        var produto = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Lanche");
        var lancado = await ambiente.LancarAsync(1, Item(produto.Id, 3, grupo.Opcoes.Single().Id));
        var item = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single();
        await cardapio.SalvarGrupoAsync(grupo.Id, grupo.Nome, true, true, ["Assado"], SetorImpressao.BarCopa);
        produto.SetorImpressaoId = SetorImpressao.BarCopa;
        await cardapio.SalvarProdutoAsync(produto, [grupo.Id]);

        var reimpresso = await ambiente.Pedidos.ReimprimirPedidoAsync(lancado.PedidoId);
        await ambiente.Pedidos.TransferirItemAsync(item.Id, 1, 2, null);
        var transferido = (await ambiente.Mesas.ObterDetalheAsync(2)).Itens.Single();
        await ambiente.Pedidos.CancelarItemAsync(transferido.Id, 1, PinGerente, "Cliente desistiu");
        await ambiente.Pedidos.CancelarItemAsync(item.Id, 1, PinGerente, "Cliente desistiu");

        Assert.Equal(SetorImpressao.Cozinha, Assert.Single(reimpresso.SetoresImpressos).Id);
        Assert.All(ambiente.Impressoes.Listar(), t => Assert.Equal(SetorImpressao.Cozinha, t.Setor.Id));
        Assert.Equal(2, ambiente.Impressoes.Listar().Count(t => t.Conteudo.Contains("CANCELAMENTO")));
        await using var db = ambiente.NovoContexto();
        Assert.All(await db.ItensPedido.ToListAsync(), i => Assert.True(i.DestacarObservacoes));
    }

    [Fact]
    public async Task SugestaoDePessoasEDestinoNaoDependemDoTipoItem()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await cardapio.SalvarGrupoAsync(0, "Pessoa", false, false, [], sugerePessoas: true);
        var grupo = (await cardapio.ListarGruposAsync()).Single(g => g.Nome == "Pessoa");
        await cardapio.SalvarProdutoAsync(new Produto
        {
            Nome = "Menu degustação", Preco = 60m, CategoriaId = 3, SetorImpressaoId = SetorImpressao.Cozinha
        }, [grupo.Id]);
        var menu = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Menu degustação");
        await ambiente.LancarAsync(1, Item(menu.Id, 2));

        Assert.Equal(2, (await ambiente.Mesas.ObterDetalheAsync(1)).QuantidadePessoas);
        Assert.Single(ambiente.Impressoes.Listar());
        var rodizio = (await cardapio.ListarProdutosAsync()).Single(p => p.Id == RodizioAdulto);
        rodizio.SetorImpressaoId = SetorImpressao.Cozinha;
        await cardapio.SalvarProdutoAsync(rodizio, []);
        var resultado = await ambiente.LancarAsync(2, Item(RodizioAdulto, 3));
        Assert.Equal(SetorImpressao.Cozinha, Assert.Single(resultado.SetoresImpressos).Id);
        Assert.Equal(0, (await ambiente.Mesas.ObterDetalheAsync(2)).QuantidadePessoas);
    }

    [Fact]
    public async Task GrupoOpcionalNaoSelecionadoNaoRedirecionaItem()
    {
        await using var ambiente = await CriarAsync();
        var cardapio = ambiente.Servico<CardapioService>();
        await cardapio.SalvarGrupoAsync(0, "Opcional", false, true, ["Alternativa"], SetorImpressao.Cozinha);
        var grupo = (await cardapio.ListarGruposAsync()).Single(g => g.Nome == "Opcional");
        await cardapio.SalvarProdutoAsync(new Produto
        {
            Nome = "Bebida", Preco = 10m, CategoriaId = 4, SetorImpressaoId = SetorImpressao.BarCopa
        }, [grupo.Id]);
        var produto = (await cardapio.ListarProdutosAsync()).Single(p => p.Nome == "Bebida");

        var resultado = await ambiente.LancarAsync(1, Item(produto.Id));

        Assert.Equal(SetorImpressao.BarCopa, Assert.Single(resultado.SetoresImpressos).Id);
    }

    [Fact]
    public async Task AlterarDestaqueDoSetorNaoMudaObservacoesDePedidoAntigo()
    {
        await using var ambiente = await CriarAsync();
        var resultado = await ambiente.Pedidos.LancarPedidoAsync(
            new NovoPedido(1, null, Garcom, [new ItemNovoPedido(Chopp, 1, [], "gelado")]));
        await using (var db = ambiente.NovoContexto())
        {
            var setor = await db.SetoresImpressao.SingleAsync(s => s.Id == SetorImpressao.BarCopa);
            setor.ObservacoesEmDestaque = true;
            await db.SaveChangesAsync();
        }

        await ambiente.Pedidos.ReimprimirPedidoAsync(resultado.PedidoId);

        Assert.All(ambiente.Impressoes.Listar(), t =>
        {
            Assert.Contains("gelado", t.Conteudo);
            Assert.DoesNotContain("GELADO", t.Conteudo);
        });
    }
}
