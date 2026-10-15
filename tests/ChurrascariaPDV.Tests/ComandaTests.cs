using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class ComandaTests
{
    [Fact]
    public async Task CancelarComandaVaziaLiberaCartaoEVoltaMesaParaLivre()
    {
        await using var ambiente = await CriarAsync();
        var comandaId = await ambiente.Mesas.CriarComandaAsync(1, 105, "Ana");
        Assert.Equal(StatusMesa.Ocupada, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);

        await ambiente.Mesas.CancelarComandaVaziaAsync(1, comandaId);

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(StatusMesa.Livre, mesa.Status);
        Assert.Empty(mesa.Comandas);
        Assert.Equal(0m, mesa.Resumo.Saldo);

        await using (var db = ambiente.NovoContexto())
        {
            var cancelada = await db.Comandas.FindAsync(comandaId);
            Assert.Equal(StatusComanda.Cancelada, cancelada!.Status);
            Assert.NotNull(cancelada.DataHoraFechamento);
        }

        // O número do cartão volta a ficar livre para uma nova comanda.
        var recriadaId = await ambiente.Mesas.CriarComandaAsync(1, 105, "Ana");
        await using (var db = ambiente.NovoContexto())
        {
            var recriada = await db.Comandas.FindAsync(recriadaId);
            Assert.Equal(105, recriada!.CodigoNumero);
            Assert.Equal(StatusComanda.Aberta, recriada.Status);
        }
    }

    [Fact]
    public async Task ComandaComItensLancadosNaoPodeSerCancelada()
    {
        await using var ambiente = await CriarAsync();
        var comandaId = await ambiente.Mesas.CriarComandaAsync(1, null, null);
        await ambiente.Pedidos.LancarPedidoAsync(new NovoPedido(1, comandaId, Garcom, [Item(Chopp)]));

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Mesas.CancelarComandaVaziaAsync(1, comandaId));
        Assert.Contains("sem itens", erro.Message);

        await using var db = ambiente.NovoContexto();
        Assert.Equal(StatusComanda.Aberta, (await db.Comandas.FindAsync(comandaId))!.Status);
    }

    [Fact]
    public async Task ContaGeralNaoPodeSerCancelada()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(Chopp));
        var contaGeral = (await ambiente.Mesas.ObterDetalheAsync(1)).Comandas.Single(c => c.ContaGeral);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Mesas.CancelarComandaVaziaAsync(1, contaGeral.Id));
        Assert.Contains("conta geral", erro.Message);
    }

    [Fact]
    public async Task ComandaDeOutraMesaNaoPodeSerCancelada()
    {
        await using var ambiente = await CriarAsync();
        var comandaDaMesa2 = await ambiente.Mesas.CriarComandaAsync(2, null, null);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Mesas.CancelarComandaVaziaAsync(1, comandaDaMesa2));
        Assert.Contains("não encontrada", erro.Message);
        Assert.Equal(StatusMesa.Ocupada, (await ambiente.Mesas.ObterDetalheAsync(2)).Status);
    }

    [Fact]
    public async Task CancelarComandaDeMesaComConsumoAnteriorVoltaParaLimpeza()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.AbrirCaixaAsync();
        await ambiente.LancarAsync(1, Item(Chopp));
        var saldo = (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Saldo;
        await ambiente.PagarAsync(1, saldo);
        Assert.Equal(StatusMesa.AguardandoLimpeza, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);

        var comandaId = await ambiente.Mesas.CriarComandaAsync(1, null, "Engano");
        Assert.Equal(StatusMesa.Ocupada, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);

        await ambiente.Mesas.CancelarComandaVaziaAsync(1, comandaId);

        Assert.Equal(StatusMesa.AguardandoLimpeza, (await ambiente.Mesas.ObterDetalheAsync(1)).Status);
    }
}
