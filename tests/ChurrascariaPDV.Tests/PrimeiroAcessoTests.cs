using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class PrimeiroAcessoTests
{
    [Fact]
    public async Task MigracaoPreservaUsuariosEExigeTrocaAntesDeRestaurarOuAutorizar()
    {
        await using var ambiente = await CriarAsync();
        await using var db = ambiente.NovoContexto();
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261015160026_SaboresEPrecosModificadores");
        await migrator.MigrateAsync();
        var usuarios = ambiente.Servico<UsuarioService>();
        var login = await usuarios.AutenticarAsync(PinGerente);
        Assert.True(login!.TrocaPinObrigatoria);
        Assert.False(login.Gerente);
        Assert.Null(await usuarios.ObterAsync(Gerente));
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Caixa_.AbrirTurnoAsync(0, Gerente));
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.LancarAsync(1, Item(Chopp)));
        await Assert.ThrowsAsync<RegraNegocioException>(() => UsuarioService.ValidarGerenteAsync(db, PinGerente));
        await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.TrocarPinAsync(Gerente, PinGerente, PinGerente, PinGerente));
        await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.TrocarPinAsync(Gerente, PinGerente, "2222", "2222"));
        await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.TrocarPinAsync(Gerente, "9999", "5678", "5678"));
        var novo = await usuarios.TrocarPinAsync(Gerente, PinGerente, "5678", "5678");
        Assert.True(novo.Gerente);
        Assert.Null(await usuarios.AutenticarAsync(PinGerente));
        Assert.NotNull(await usuarios.ObterAsync(Gerente));
        Assert.True((await usuarios.AutenticarAsync("5678"))!.Gerente);
        db.ChangeTracker.Clear();
        Assert.Equal(Gerente, (await UsuarioService.ValidarGerenteAsync(db, "5678")).Id);
        await using var leitura = ambiente.NovoContexto();
        Assert.NotEqual("5678", (await leitura.Usuarios.FindAsync(Gerente))!.PinAcesso);
        Assert.Equal(4, await leitura.Usuarios.CountAsync());
    }

    [Theory]
    [InlineData(TipoEstabelecimento.Churrascaria)]
    [InlineData(TipoEstabelecimento.Pizzaria)]
    [InlineData(TipoEstabelecimento.Hamburgueria)]
    [InlineData(TipoEstabelecimento.Bar)]
    [InlineData(TipoEstabelecimento.RestaurantePorQuilo)]
    public async Task CadaPerfilTemExemplosSemUsuariosOuPinsPadrao(TipoEstabelecimento tipo)
    {
        await using var ambiente = await CriarAsync();
        await using var db = ambiente.NovoContexto();
        await SeedPerfil.AplicarAsync(db, new InstalacaoOptions { Nome = "Casa de teste", Tipo = tipo });
        Assert.Empty(await db.Usuarios.ToListAsync());
        var perfil = await db.PerfisEstabelecimento.SingleAsync();
        Assert.Equal("Casa de teste", perfil.Nome);
        Assert.Equal(tipo, perfil.Tipo);
        Assert.NotEmpty(await db.Produtos.ToListAsync());
        Assert.NotEmpty(await db.CategoriasProdutos.ToListAsync());
        Assert.Single(await db.SetoresImpressao.Where(s => s.RecebeContas).ToListAsync());
        if (tipo != TipoEstabelecimento.Churrascaria)
        {
            Assert.DoesNotContain(await db.Produtos.ToListAsync(), p => p.TipoItem == TipoItem.Rodizio);
            Assert.DoesNotContain(await db.GruposModificadores.ToListAsync(), g => g.Nome == "Ponto da Carne");
            Assert.False(await db.SetoresImpressao.AnyAsync(s => s.Nome == "Churrasqueira"));
        }
        if (tipo == TipoEstabelecimento.Pizzaria)
            Assert.True(await db.GruposModificadores.AnyAsync(g => g.Sabores));
        await ambiente.Servico<UsuarioService>().CriarGerenteInicialAsync("Dono", "5876");
        Assert.True((await ambiente.Servico<UsuarioService>().AutenticarAsync("5876"))!.Gerente);
        if (tipo == TipoEstabelecimento.RestaurantePorQuilo)
        {
            var buffet = (await ambiente.Servico<CardapioService>().ObterCardapioAsync()).SelectMany(c => c.Produtos)
                .Single(p => p.QuantidadeFracionada);
            var dono = await ambiente.Servico<UsuarioService>().AutenticarAsync("5876");
            await ambiente.Pedidos.LancarPedidoAsync(new NovoPedido(1, null, dono!.Id, [Item(buffet.Id, 0.5m)]));
            Assert.Equal(34.95m, (await ambiente.Mesas.ObterDetalheAsync(1)).Resumo.Subtotal);
        }
        await Assert.ThrowsAsync<RegraNegocioException>(() => ambiente.Servico<UsuarioService>().CriarGerenteInicialAsync("Outro", "9876"));
        await Assert.ThrowsAsync<RegraNegocioException>(() => SeedPerfil.AplicarAsync(db, new InstalacaoOptions()));
    }

    [Fact]
    public async Task UsuarioNovoOuPinRedefinidoExigeTrocaMasEdicaoDeNomeNao()
    {
        await using var ambiente = await CriarAsync();
        var servico = ambiente.Servico<UsuarioService>();
        await servico.SalvarAsync(new Usuario { Nome = "Novo", Perfil = PerfilUsuario.Garcom, PinAcesso = "6789" });
        var login = await servico.AutenticarAsync("6789");
        Assert.True(login!.TrocaPinObrigatoria);
        await servico.TrocarPinAsync(login.Id, "6789", "7890", "7890");
        await servico.SalvarAsync(new Usuario { Id = login.Id, Nome = "Renomeado", Perfil = PerfilUsuario.Garcom });
        Assert.False((await servico.AutenticarAsync("7890"))!.TrocaPinObrigatoria);
        await servico.SalvarAsync(new Usuario { Id = login.Id, Nome = "Renomeado", Perfil = PerfilUsuario.Garcom, PinAcesso = "8901" });
        Assert.True((await servico.AutenticarAsync("8901"))!.TrocaPinObrigatoria);
    }
}
