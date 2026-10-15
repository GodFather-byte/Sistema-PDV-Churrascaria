using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class PerfilEstabelecimentoTests
{
    [Fact]
    public async Task MigrationCriaPerfilPadraoDeChurrascaria()
    {
        await using var ambiente = await CriarAsync();
        await using var db = ambiente.NovoContexto();

        var perfil = await db.PerfisEstabelecimento.FindAsync(1);

        Assert.NotNull(perfil);
        Assert.Equal(TipoEstabelecimento.Churrascaria, perfil.Tipo);
        Assert.Equal("CHURRASCARIA", perfil.Nome);
        Assert.Equal(10m, perfil.PercentualTaxaServico);
        Assert.True(perfil.TaxaServicoOpcionalPorComanda);
        Assert.Equal(48, perfil.LarguraTicket);
    }

    [Fact]
    public async Task PercentualConfiguradoEAplicadoNaConta()
    {
        await using var ambiente = await CriarAsync();
        await using (var db = ambiente.NovoContexto())
        {
            var perfil = await db.PerfisEstabelecimento.FindAsync(1);
            Assert.NotNull(perfil);
            perfil.PercentualTaxaServico = 12.5m;
            await db.SaveChangesAsync();
        }

        await ambiente.LancarAsync(1, Item(RodizioAdulto));

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(11.24m, mesa.Resumo.TaxaServico);
        Assert.Equal(101.14m, mesa.Resumo.Total);
        Assert.Equal(12.5m, mesa.PercentualTaxaServico);
    }

    [Fact]
    public async Task TaxaObrigatoriaNaoPodeSerDesativadaPorComanda()
    {
        await using var ambiente = await CriarAsync();
        await using (var db = ambiente.NovoContexto())
        {
            var perfil = await db.PerfisEstabelecimento.FindAsync(1);
            Assert.NotNull(perfil);
            perfil.PercentualTaxaServico = 15m;
            perfil.TaxaServicoOpcionalPorComanda = false;
            await db.SaveChangesAsync();
        }

        await ambiente.LancarAsync(1, Item(RodizioAdulto));
        var comandaId = (await ambiente.Mesas.ObterDetalheAsync(1)).Comandas.Single().Id;
        await using (var db = ambiente.NovoContexto())
        {
            var comanda = await db.Comandas.FindAsync(comandaId);
            Assert.NotNull(comanda);
            comanda.CobrarTaxaServico = false;
            await db.SaveChangesAsync();
        }

        var mesa = await ambiente.Mesas.ObterDetalheAsync(1);
        Assert.Equal(13.49m, mesa.Resumo.TaxaServico);
        Assert.False(mesa.TaxaServicoOpcionalPorComanda);
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            ambiente.Mesas.DefinirTaxaServicoAsync(1, comandaId, false));
    }

    [Fact]
    public async Task PerfilImpedeAlterarTaxaComComandasAbertas()
    {
        await using var ambiente = await CriarAsync();
        await ambiente.LancarAsync(1, Item(RodizioAdulto));
        var perfilService = ambiente.Servico<PerfilEstabelecimentoService>();
        var perfil = await perfilService.ObterAsync();
        perfil.PercentualTaxaServico = 15m;

        await Assert.ThrowsAsync<RegraNegocioException>(() => perfilService.SalvarAsync(perfil));
    }
}
