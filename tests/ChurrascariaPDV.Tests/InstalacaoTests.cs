using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChurrascariaPDV.Tests;

public class InstalacaoTests
{
    [Fact]
    public async Task UpgradeProtegePinsLegadosEPreservaDadosSemReaplicarSeed()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"pdv-instalacao-{Guid.NewGuid():N}.db");
        try
        {
            await using var servicos = Servicos(arquivo, "Pizzaria", "Nao deve substituir o legado");
            var factory = servicos.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.GetService<IMigrator>().MigrateAsync("20261015160026_SaboresEPrecosModificadores");
                await db.Database.ExecuteSqlRawAsync("UPDATE Produtos SET Nome = 'Produto personalizado', Preco = '123.45' WHERE Id = 3;");
            }
            await BancoDeDados.PrepararAsync(servicos, NullLogger.Instance);
            await using var leitura = await factory.CreateDbContextAsync();
            var personalizado = await leitura.Produtos.SingleAsync(p => p.Id == 3);
            Assert.Equal("Produto personalizado", personalizado.Nome);
            Assert.Equal(123.45m, personalizado.Preco);
            Assert.Equal(Domain.Enums.TipoEstabelecimento.Churrascaria, (await leitura.PerfisEstabelecimento.SingleAsync()).Tipo);
            Assert.All(await leitura.Usuarios.ToListAsync(), u =>
            {
                Assert.True(u.TrocaPinObrigatoria);
                Assert.False(UsuarioService.PinValido(u.PinAcesso));
            });
            var usuarios = new UsuarioService(factory);
            Assert.True((await usuarios.AutenticarAsync(AmbienteTeste.PinGerente))!.TrocaPinObrigatoria);
            await usuarios.TrocarPinAsync(AmbienteTeste.Gerente, AmbienteTeste.PinGerente, "8765", "8765");
            await BancoDeDados.PrepararAsync(servicos, NullLogger.Instance);
            Assert.False((await usuarios.AutenticarAsync("8765"))!.TrocaPinObrigatoria);
        }
        finally { Limpar(arquivo); }
    }

    [Fact]
    public async Task StartupAplicaPerfilUmaVezEPreservaEdicoesEUsuariosNasAtualizacoes()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"pdv-instalacao-{Guid.NewGuid():N}.db");
        try
        {
            await using (var servicos = Servicos(arquivo, "Pizzaria", "Pizza São João"))
            {
                await BancoDeDados.PrepararAsync(servicos, NullLogger.Instance);
                var factory = servicos.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var db = await factory.CreateDbContextAsync();
                Assert.Empty(await db.Usuarios.ToListAsync());
                Assert.True(await db.GruposModificadores.AnyAsync(g => g.Sabores));
                var pizza = await db.Produtos.SingleAsync(p => p.Nome == "Pizza grande");
                pizza.Nome = "Pizza personalizada";
                await db.SaveChangesAsync();
                await new UsuarioService(factory).CriarGerenteInicialAsync("Proprietário", "9876");
            }
            await using (var servicos = Servicos(arquivo, "Bar", "Outro nome que não deve sobrescrever"))
            {
                await BancoDeDados.PrepararAsync(servicos, NullLogger.Instance);
                await using var db = await servicos.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
                Assert.Equal("Pizza São João", (await db.PerfisEstabelecimento.SingleAsync()).Nome);
                Assert.True(await db.Produtos.AnyAsync(p => p.Nome == "Pizza personalizada"));
                var dono = await db.Usuarios.SingleAsync();
                Assert.NotEqual("9876", dono.PinAcesso);
                Assert.False(dono.TrocaPinObrigatoria);
            }
            Assert.False(File.Exists($"{arquivo}.seed-pendente"));
        }
        finally { Limpar(arquivo); }
    }

    [Fact]
    public async Task SeedInterrompidoMantemMarcadorEReinicializacaoRecuperaSemPinsPadrao()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"pdv-instalacao-{Guid.NewGuid():N}.db");
        try
        {
            await using (var servicos = Servicos(arquivo, "Bar", ""))
            {
                await Assert.ThrowsAsync<RegraNegocioException>(() => BancoDeDados.PrepararAsync(servicos, NullLogger.Instance));
            }
            Assert.True(File.Exists($"{arquivo}.seed-pendente"));
            await using (var servicos = Servicos(arquivo, "Bar", "Bar recuperado"))
            {
                await BancoDeDados.PrepararAsync(servicos, NullLogger.Instance);
                await using var db = await servicos.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
                Assert.Empty(await db.Usuarios.ToListAsync());
                Assert.Equal("Bar recuperado", (await db.PerfisEstabelecimento.SingleAsync()).Nome);
            }
            Assert.False(File.Exists($"{arquivo}.seed-pendente"));
        }
        finally { Limpar(arquivo); }
    }

    private static ServiceProvider Servicos(string arquivo, string tipo, string nome)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Instalacao:Tipo"] = tipo, ["Instalacao:Nome"] = nome
        }).Build());
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={arquivo};Pooling=False"));
        return services.BuildServiceProvider();
    }

    private static void Limpar(string arquivo)
    {
        foreach (var sufixo in new[] { "", "-wal", "-shm", ".seed-pendente" })
            File.Delete(arquivo + sufixo);
    }
}
