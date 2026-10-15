using Microsoft.EntityFrameworkCore;

namespace LicencasServidor.Data;

/// <summary>Uma licença por PC de caixa. O cliente com duas lojas tem duas licenças.</summary>
public class LicencaCliente
{
    public string Id { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string? Contato { get; set; }
    public string? Observacoes { get; set; }
    public string Maquina { get; set; } = string.Empty;
    public DateOnly ValidaAte { get; set; }
    public bool Bloqueada { get; set; }
    public string? MotivoBloqueio { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public DateTime? UltimaVerificacao { get; set; }
    public string? UltimaVersao { get; set; }
    public string? UltimoIp { get; set; }
    public int TotalVerificacoes { get; set; }
}

public class Administrador
{
    public int Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public class LicencasDbContext(DbContextOptions<LicencasDbContext> options) : DbContext(options)
{
    public DbSet<LicencaCliente> Licencas => Set<LicencaCliente>();
    public DbSet<Administrador> Administradores => Set<Administrador>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LicencaCliente>(entity =>
        {
            entity.Property(l => l.Id).HasMaxLength(12);
            entity.Property(l => l.Cliente).HasMaxLength(120);
            entity.Property(l => l.Contato).HasMaxLength(200);
            entity.Property(l => l.Observacoes).HasMaxLength(1000);
            entity.Property(l => l.Maquina).HasMaxLength(19);
            entity.Property(l => l.MotivoBloqueio).HasMaxLength(250);
            entity.Property(l => l.UltimaVersao).HasMaxLength(40);
            entity.Property(l => l.UltimoIp).HasMaxLength(45);
            entity.HasIndex(l => l.Maquina);
        });
        modelBuilder.Entity<Administrador>(entity =>
        {
            entity.Property(a => a.Usuario).HasMaxLength(60);
            entity.HasIndex(a => a.Usuario).IsUnique();
        });
    }
}
