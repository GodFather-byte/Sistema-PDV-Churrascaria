using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class ProtecaoPinTests
{
    private sealed class RelogioManual : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    [Fact]
    public async Task BloqueiaAparelhoAposFalhasSeguidasEReabreDepoisDoTempo()
    {
        await using var ambiente = await CriarAsync();
        var relogio = new RelogioManual();
        var usuarios = new UsuarioService(ambiente.Servico<IDbContextFactory<AppDbContext>>(),
            new ProtecaoPin(new LimiteTentativasPin(relogio), new HttpContextAccessor()));

        for (var i = 0; i < LimiteTentativasPin.TentativasLivres; i++)
            Assert.Null(await usuarios.AutenticarAsync("9999"));

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.AutenticarAsync(PinGerente));
        Assert.Contains("Aguarde 30 s", erro.Message);

        relogio.Agora += TimeSpan.FromSeconds(31);
        Assert.Null(await usuarios.AutenticarAsync("9999"));
        await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.AutenticarAsync(PinGerente));

        relogio.Agora += TimeSpan.FromSeconds(61);
        Assert.NotNull(await usuarios.AutenticarAsync(PinGerente));
        Assert.Null(await usuarios.AutenticarAsync("9999"));
    }

    [Fact]
    public async Task PinDeGerenteErradoContaComoFalha()
    {
        await using var ambiente = await CriarAsync();
        var protecao = new ProtecaoPin(new LimiteTentativasPin(new RelogioManual()), new HttpContextAccessor());
        await using var db = ambiente.NovoContexto();

        for (var i = 0; i < LimiteTentativasPin.TentativasLivres; i++)
            await Assert.ThrowsAsync<RegraNegocioException>(() => UsuarioService.ValidarGerenteAsync(db, "9999", protecao));

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => UsuarioService.ValidarGerenteAsync(db, PinGerente, protecao));
        Assert.Contains("Muitas tentativas", erro.Message);
    }

    [Fact]
    public async Task BackupGeraCopiaLegivelEMantemSomenteAsMaisRecentes()
    {
        await using var ambiente = await CriarAsync();
        var pasta = Path.Combine(Path.GetTempPath(), "pdv-backup-" + Guid.NewGuid().ToString("N"));
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backup:Pasta"] = pasta,
            ["Backup:Retencao"] = "2"
        }).Build();
        var backup = new BackupBancoService(ambiente.Servico<IDbContextFactory<AppDbContext>>(), configuracao,
            NullLogger<BackupBancoService>.Instance);

        try
        {
            string ultimo = string.Empty;
            for (var i = 0; i < 3; i++)
            {
                ultimo = await backup.CriarBackupAsync();
                await Task.Delay(5);
            }

            Assert.Equal(2, Directory.GetFiles(pasta, "pdv-*.db").Length);
            var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={ultimo};Pooling=False").Options;
            await using var copia = new AppDbContext(opcoes);
            Assert.True(await copia.Usuarios.AnyAsync());
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }
}
