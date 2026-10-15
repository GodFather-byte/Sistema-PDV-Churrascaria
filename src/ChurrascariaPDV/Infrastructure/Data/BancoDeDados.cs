using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Infrastructure.Data;

public static class BancoDeDados
{
    /// <summary>Cria/atualiza o banco SQLite via migrations e ativa o modo WAL para acessos simultâneos.</summary>
    public static async Task PrepararAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        await PreservarBancoLegadoAsync(db, logger);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }

    /// <summary>
    /// Bancos gerados pela primeira versão (EnsureCreated) não têm histórico de migrations.
    /// Eles são renomeados como backup para que um banco novo seja criado.
    /// </summary>
    private static async Task PreservarBancoLegadoAsync(AppDbContext db, ILogger logger)
    {
        var conexao = db.Database.GetDbConnection();
        var arquivo = new SqliteConnectionStringBuilder(conexao.ConnectionString).DataSource;
        if (string.IsNullOrWhiteSpace(arquivo) || arquivo == ":memory:" || !File.Exists(arquivo))
        {
            return;
        }

        await conexao.OpenAsync();
        bool legado;
        await using (var comando = conexao.CreateCommand())
        {
            comando.CommandText =
                "SELECT (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Mesas') > 0 " +
                "AND (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory') = 0";
            legado = Convert.ToBoolean(await comando.ExecuteScalarAsync());
        }

        await conexao.CloseAsync();
        if (!legado)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        var backup = $"{arquivo}.legado-{DateTime.Now:yyyyMMddHHmmss}.bak";
        File.Move(arquivo, backup);
        logger.LogWarning("Banco antigo sem migrations movido para {Backup}. Um banco novo será criado.", backup);
    }
}
