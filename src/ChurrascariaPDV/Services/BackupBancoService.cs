using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

/// <summary>
/// Copia o banco SQLite para a pasta de backups ao iniciar e a cada 24 horas, mantendo as cópias mais recentes.
/// Usa VACUUM INTO, que gera uma cópia consistente mesmo com o caixa em uso.
/// </summary>
public class BackupBancoService(IDbContextFactory<AppDbContext> dbFactory, IConfiguration configuration,
    ILogger<BackupBancoService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private string Pasta => configuration["Backup:Pasta"] ?? "backups";
    private int Retencao => Math.Max(1, configuration.GetValue("Backup:Retencao", 14));

    protected override async Task ExecuteAsync(CancellationToken cancelamento)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                var arquivo = await CriarBackupAsync(cancelamento);
                logger.LogInformation("Backup do banco salvo em {Arquivo}", arquivo);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao gerar o backup do banco em {Pasta}", Pasta);
            }
        } while (await timer.WaitForNextTickAsync(cancelamento));
    }

    public async Task<string> CriarBackupAsync(CancellationToken cancelamento = default)
    {
        var pasta = Path.GetFullPath(Pasta);
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"pdv-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");

        await using (var db = await dbFactory.CreateDbContextAsync(cancelamento))
        {
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {arquivo}", cancelamento);
        }

        foreach (var antigo in new DirectoryInfo(pasta).GetFiles("pdv-*.db")
                     .OrderByDescending(f => f.Name).Skip(Retencao))
        {
            antigo.Delete();
        }

        return arquivo;
    }
}
