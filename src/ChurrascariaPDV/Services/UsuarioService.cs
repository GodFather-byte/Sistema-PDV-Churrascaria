using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record UsuarioLogado(int Id, string Nome, PerfilUsuario Perfil)
{
    public bool PodeOperarCaixa => Perfil is PerfilUsuario.OperadorCaixa or PerfilUsuario.Gerente;
    public bool Gerente => Perfil == PerfilUsuario.Gerente;
}

public class UsuarioService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<UsuarioLogado?> AutenticarAsync(string pin)
    {
        if (!PinValido(pin))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PinAcesso == pin && u.Ativo);
        return usuario is null ? null : new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Perfil);
    }

    public async Task<UsuarioLogado?> ObterAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Ativo);
        return usuario is null ? null : new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Perfil);
    }

    /// <summary>Valida o PIN de um gerente ativo, exigido para cancelamentos e estornos.</summary>
    public static async Task<Usuario> ValidarGerenteAsync(AppDbContext db, string? pinGerente)
    {
        if (string.IsNullOrWhiteSpace(pinGerente))
        {
            throw new RegraNegocioException("Informe o PIN do gerente.");
        }

        var gerente = await db.Usuarios.FirstOrDefaultAsync(u => u.PinAcesso == pinGerente && u.Ativo);
        if (gerente is null || gerente.Perfil != PerfilUsuario.Gerente)
        {
            throw new RegraNegocioException("PIN de gerente inválido.");
        }

        return gerente;
    }

    public async Task<List<Usuario>> ListarAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Usuarios.AsNoTracking().OrderBy(u => u.Perfil).ThenBy(u => u.Nome).ToListAsync();
    }

    public async Task SalvarAsync(Usuario dados)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            throw new RegraNegocioException("Informe o nome do usuário.");
        }

        if (!PinValido(dados.PinAcesso))
        {
            throw new RegraNegocioException("O PIN deve ter exatamente 4 dígitos numéricos.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Usuarios.AnyAsync(u => u.PinAcesso == dados.PinAcesso && u.Id != dados.Id))
        {
            throw new RegraNegocioException("Já existe um usuário com esse PIN.");
        }

        var usuario = dados.Id == 0 ? new Usuario() : await db.Usuarios.FindAsync(dados.Id)
            ?? throw new RegraNegocioException("Usuário não encontrado.");

        if (usuario.Perfil == PerfilUsuario.Gerente && (dados.Perfil != PerfilUsuario.Gerente || !dados.Ativo)
            && !await db.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.Perfil == PerfilUsuario.Gerente && u.Ativo))
        {
            throw new RegraNegocioException("O sistema precisa de pelo menos um gerente ativo.");
        }

        usuario.Nome = dados.Nome.Trim();
        usuario.PinAcesso = dados.PinAcesso;
        usuario.Perfil = dados.Perfil;
        usuario.Ativo = dados.Ativo;
        if (usuario.Id == 0)
        {
            db.Usuarios.Add(usuario);
        }

        await db.SaveChangesAsync();
    }

    public static bool PinValido(string? pin) => pin is { Length: 4 } && pin.All(char.IsAsciiDigit);
}
