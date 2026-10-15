using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class PedidoServiceTests
{
    [Fact]
    public async Task PrimeiroItemAbreMesaAutomaticamente()
    {
        await using var ambiente = await CriarAsync();

        await ambiente.LancarAsync(1, Item(RodizioAdulto, 2));

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(StatusMesa.Ocupada, mesa.Status);
        Assert.NotNull(mesa.DataHoraAbertura);
        Assert.Equal(2, mesa.QuantidadePessoas);
        Assert.Equal(179.80m, mesa.Resumo.Subtotal);
        Assert.Equal(17.98m, mesa.Resumo.TaxaServico);
        Assert.Single(mesa.Comandas);
        Assert.True(mesa.Comandas[0].ContaGeral);
    }

    [Fact]
    public async Task RodizioNaoImprimeEItensVaoParaOSetorCorreto()
    {
        await using var ambiente = await CriarAsync();

        var resultado = await ambiente.LancarAsync(1,
            Item(RodizioAdulto, 2),
            Item(Chopp, 2, ComGeloLimao),
            Item(PolentaFrita),
            Item(Picanha, 1, AoPonto));

        Assert.True(resultado.ImpressoComSucesso);
        Assert.Equal([SetorImpressao.BarCopa, SetorImpressao.Cozinha, SetorImpressao.Churrasqueira], resultado.SetoresImpressos);

        var tickets = ambiente.Impressoes.Listar();
        Assert.Equal(3, tickets.Count);
        var bar = tickets.Single(t => t.Setor == SetorImpressao.BarCopa).Conteudo;
        Assert.Contains("Chopp 500ml", bar);
        Assert.Contains("Com Gelo e Limão", bar);
        Assert.Contains("João", bar);
        Assert.DoesNotContain("Rodízio", bar);
        var churrasqueira = tickets.Single(t => t.Setor == SetorImpressao.Churrasqueira).Conteudo;
        Assert.Contains("AO PONTO", churrasqueira);
        Assert.DoesNotContain(tickets, t => t.Conteudo.Contains("Rodízio Completo"));
    }

    [Fact]
    public async Task ModificadorObrigatorioDeveSerInformado()
    {
        await using var ambiente = await CriarAsync();

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.LancarAsync(1, Item(Picanha)));

        Assert.Contains("Ponto da Carne", erro.Message);
        Assert.Equal(StatusMesa.Livre, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
    }

    [Fact]
    public async Task CancelamentoExigePinDeGerenteEMotivo()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(Chopp, 3));
        var item = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single();

        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Pedidos.CancelarItemAsync(item.Id, 1, "1111", "Cliente desistiu"));
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Pedidos.CancelarItemAsync(item.Id, 1, PinGerente, ""));

        await ambiente.Pedidos.CancelarItemAsync(item.Id, 1, PinGerente, "Cliente desistiu");

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(2, mesa.Itens.Single(i => !i.Cancelado).Quantidade);
        var cancelado = mesa.Itens.Single(i => i.Cancelado);
        Assert.Equal(1, cancelado.Quantidade);
        Assert.Equal("Cliente desistiu", cancelado.MotivoCancelamento);
        Assert.Equal(29.80m, mesa.Resumo.Subtotal);
        Assert.Contains(ambiente.Impressoes.Listar(), t => t.Conteudo.Contains("CANCELAMENTO"));
    }

    [Fact]
    public async Task TransfereItemParcialEntreComandas()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(SucoJarra, 2));
        var comanda101 = await ambiente.Mesas.CriarComandaAsync(1, 101, "Ana");
        var item = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single();

        await ambiente.Pedidos.TransferirItemAsync(item.Id, 1, 1, comanda101);

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(1, mesa.Comandas.Single(c => c.ContaGeral).Itens.Single().Quantidade);
        Assert.Equal(1, mesa.Comandas.Single(c => c.Id == comanda101).Itens.Single().Quantidade);
        Assert.Equal(45.80m, mesa.Resumo.Subtotal);
    }

    [Fact]
    public async Task TransfereItemInteiroParaOutraMesaLivre()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(SucoJarra), Item(Chopp));
        var suco = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single(i => i.ProdutoId == SucoJarra);

        await ambiente.Pedidos.TransferirItemAsync(suco.Id, 1, 2, null);

        var origem = await ambiente.Mesas.ObterDetalheAsync(1);
        var destino = await ambiente.Mesas.ObterDetalheAsync(2);
        Assert.Equal(Chopp, origem.Itens.Single().ProdutoId);
        Assert.Equal(StatusMesa.Ocupada, destino.Status);
        Assert.Equal(SucoJarra, destino.Itens.Single().ProdutoId);

        await using var db = ambiente.NovoContexto();
        Assert.Equal(2, await db.ItensPedido.CountAsync());
    }

    [Fact]
    public async Task NovoItemDepoisDaPreContaVoltaMesaParaOcupada()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto));
        await ambiente.Mesas.SolicitarContaAsync(1);
        Assert.Equal(StatusMesa.ContaSolicitada, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
        Assert.Contains(ambiente.Impressoes.Listar(), t => t.Setor == SetorImpressao.Caixa && t.Conteudo.Contains("CONFERÊNCIA"));

        await ambiente.LancarAsync(1, Item(Chopp));

        Assert.Equal(StatusMesa.Ocupada, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
    }
}
