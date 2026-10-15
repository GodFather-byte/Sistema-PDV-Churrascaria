using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class MesaEPagamentoTests
{
    [Fact]
    public async Task PagamentoParcialAbateSaldoEFinalizaQuandoZera()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        // 4 rodízios de 89,90 = 359,60 + 10% (35,96) = 395,56
        await ambiente.LancarAsync(1, Item(RodizioAdulto, 4));

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(395.56m, mesa.Resumo.Total);
        Assert.Equal(98.89m, mesa.Resumo.ValorPorPessoa(4));

        var parcial = await ambiente.PagarAsync(1, 100m);
        Assert.False(parcial.MesaFinalizada);
        Assert.Equal(295.56m, parcial.SaldoRestanteMesa);
        Assert.Equal(StatusMesa.Ocupada, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);

        var final = await ambiente.PagarAsync(1, 300m, MeioPagamento.Dinheiro);
        Assert.True(final.MesaFinalizada);
        Assert.Equal(4.44m, final.Troco);

        var depois = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(StatusMesa.AguardandoLimpeza, depois.Status);
        Assert.Empty(depois.Comandas);

        await ambiente.Mesas.LiberarMesaAsync(1);
        Assert.Equal(StatusMesa.Livre, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
    }

    [Fact]
    public async Task CartaoNaoAceitaValorMaiorQueSaldo()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(Chopp));

        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.PagarAsync(1, 50m, MeioPagamento.CartaoCredito));
    }

    [Fact]
    public async Task PagamentoExigeTurnoAberto()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(Chopp));

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.PagarAsync(1, 10m));
        Assert.Contains("turno", erro.Message);
    }

    [Fact]
    public async Task RemoverTaxaDeServicoRecalculaTotal()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto, 2));

        await ambiente.Mesas.DefinirTaxaServicoAsync(1, null, false);

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(0, mesa.Resumo.TaxaServico);
        Assert.Equal(179.80m, mesa.Resumo.Total);
    }

    [Fact]
    public async Task ComandaIndividualQuitadaFechaSemLiberarMesa()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto));
        var comanda = await ambiente.Mesas.CriarComandaAsync(1, null, "Carlos");
        await ambiente.Pedidos.LancarPedidoAsync(new NovoPedido(1, comanda, Garcom, [Item(Chopp)]));

        var detalhe = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(101, detalhe.Comandas.Single(c => c.Id == comanda).CodigoNumero);

        var resultado = await ambiente.PagarAsync(1, 16.39m, comandaId: comanda);

        Assert.False(resultado.MesaFinalizada);
        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(StatusMesa.Ocupada, mesa.Status);
        Assert.Single(mesa.Comandas);
        Assert.True(mesa.Comandas[0].ContaGeral);
    }

    [Fact]
    public async Task TransferenciaDeMesaLevaContaEJuntaComDestinoOcupado()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(10, Item(RodizioAdulto, 2));
        await ambiente.LancarAsync(25, Item(Chopp));

        await ambiente.Mesas.TransferirMesaAsync(10, 25);

        var origem = await ambiente.Mesas.ObterDetalheAsync(10);
        var destino = await ambiente.Mesas.ObterDetalheAsync(25);
        Assert.Equal(StatusMesa.AguardandoLimpeza, origem.Status);
        Assert.Empty(origem.Comandas);
        Assert.Single(destino.Comandas);
        Assert.Equal(2, destino.Itens.Count());
        Assert.Equal(2 * 89.90m + 14.90m, destino.Resumo.Subtotal);
        Assert.Equal(2, destino.QuantidadePessoas);
    }

    [Fact]
    public async Task CancelamentoNaoPodeDeixarPagamentoMaiorQueConsumo()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto), Item(Chopp));
        await ambiente.PagarAsync(1, 95m);
        var rodizio = (await ambiente.Mesas.ObterDetalheAsync(1)).Itens.Single(i => i.ProdutoId == RodizioAdulto);

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            ambiente.Pedidos.CancelarItemAsync(rodizio.Id, 1, PinGerente, "Erro de digitação"));
    }

    [Fact]
    public async Task EstornoDevolveSaldo()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto));
        await ambiente.PagarAsync(1, 50m);
        var pagamento = (await ambiente.Mesas.ObterDetalheAsync(1)).Comandas[0].Pagamentos.Single();

        await ambiente.Pagamentos.EstornarAsync(pagamento.Id, PinGerente, "Lançado na mesa errada");

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(0, mesa.Resumo.Pago);
        Assert.Equal(mesa.Resumo.Total, mesa.Resumo.Saldo);

        var relatorio = await ambiente.Servico<RelatorioService>().GerarAsync(DateTime.Today, DateTime.Today.AddDays(1));
        var estorno = Assert.Single(relatorio.Estornos);
        Assert.Equal(50m, estorno.Valor);
        Assert.Equal("Lançado na mesa errada", estorno.Motivo);
        Assert.False(string.IsNullOrEmpty(estorno.Gerente));
    }

    [Fact]
    public async Task FechamentoDoTurnoResumePorFormaDePagamento()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync(150m);
        await ambiente.LancarAsync(1, Item(RodizioAdulto));       // 98,89 com 10%
        await ambiente.PagarAsync(1, 50m, MeioPagamento.Pix);
        await ambiente.PagarAsync(1, 60m, MeioPagamento.Dinheiro); // troco 11,11
        await ambiente.Caixa_.RegistrarMovimentacaoAsync(TipoMovimentacaoCaixa.Sangria, 40m, "Cofre", Caixa);
        await ambiente.Caixa_.RegistrarMovimentacaoAsync(TipoMovimentacaoCaixa.Suprimento, 20m, null, Caixa);

        var resumo = await ambiente.Caixa_.FecharTurnoAsync(178.89m, null, Caixa);

        Assert.Equal(98.89m, resumo.TotalRecebido);
        Assert.Equal(50m, resumo.PorMeio.Single(m => m.Meio == MeioPagamento.Pix).Valor);
        Assert.Equal(48.89m, resumo.TotalDinheiro);
        Assert.Equal(150m + 48.89m + 20m - 40m, resumo.DinheiroEsperado);
        Assert.Equal(8.99m, resumo.TotalTaxaServico);
        Assert.Equal(1, resumo.MesasFechadas);
        Assert.Null(await ambiente.Caixa_.ObterTurnoAbertoAsync());
        Assert.Contains(ambiente.Impressoes.Listar(), t => t.Conteudo.Contains("FECHAMENTO DE CAIXA"));
    }
}
