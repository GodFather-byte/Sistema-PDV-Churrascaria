using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ChurrascariaPDV.Services;

public record UsuarioLogado(int Id, string Nome, PerfilUsuario Perfil, bool TrocaPinObrigatoria = false)
{
    public bool PodeOperarCaixa => !TrocaPinObrigatoria && Perfil is PerfilUsuario.OperadorCaixa or PerfilUsuario.Gerente;
    public bool Gerente => !TrocaPinObrigatoria && Perfil == PerfilUsuario.Gerente;
}

public class UsuarioService(IDbContextFactory<AppDbContext> dbFactory)
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    internal static string ProtegerPin(Usuario usuario, string pin) => Hasher.HashPassword(usuario, pin);

    private static bool ConferePin(Usuario usuario, string pin) => PinValido(usuario.PinAcesso)
        ? usuario.PinAcesso == pin
        : Hasher.VerifyHashedPassword(usuario, usuario.PinAcesso, pin) != PasswordVerificationResult.Failed;

    public async Task<UsuarioLogado?> AutenticarAsync(string pin)
    {
        if (!PinValido(pin))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var usuario = (await db.Usuarios.AsNoTracking().Where(u => u.Ativo).ToListAsync())
            .FirstOrDefault(u => ConferePin(u, pin));
        return usuario is null ? null : new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Perfil, usuario.TrocaPinObrigatoria);
    }

    public async Task<UsuarioLogado?> ObterAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Ativo && !u.TrocaPinObrigatoria);
        return usuario is null ? null : new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Perfil);
    }

    /// <summary>Valida o PIN de um gerente ativo, exigido para cancelamentos e estornos.</summary>
    public static async Task<Usuario> ValidarGerenteAsync(AppDbContext db, string? pinGerente)
    {
        if (string.IsNullOrWhiteSpace(pinGerente))
        {
            throw new RegraNegocioException("Informe o PIN do gerente.");
        }

        var gerente = (await db.Usuarios.Where(u => u.Ativo).ToListAsync()).FirstOrDefault(u => ConferePin(u, pinGerente));
        if (gerente is null || gerente.Perfil != PerfilUsuario.Gerente)
        {
            throw new RegraNegocioException("PIN de gerente inválido.");
        }
        if (gerente.TrocaPinObrigatoria)
        {
            throw new RegraNegocioException("O gerente precisa trocar o PIN no primeiro acesso antes de autorizar operações.");
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
        if (string.IsNullOrWhiteSpace(dados.Nome) || dados.Nome.Trim().Length > 100 || !Enum.IsDefined(dados.Perfil))
        {
            throw new RegraNegocioException("Informe um nome de até 100 caracteres e um perfil válido.");
        }

        var alterarPin = dados.Id == 0 || !string.IsNullOrEmpty(dados.PinAcesso);
        if (alterarPin && !PinValido(dados.PinAcesso))
        {
            throw new RegraNegocioException("O PIN deve ter exatamente 4 dígitos numéricos.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        if (alterarPin && (await db.Usuarios.Where(u => u.Id != dados.Id).ToListAsync()).Any(u => ConferePin(u, dados.PinAcesso)))
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
        if (alterarPin)
        {
            usuario.PinAcesso = ProtegerPin(usuario, dados.PinAcesso);
            usuario.TrocaPinObrigatoria = true;
        }
        usuario.Perfil = dados.Perfil;
        usuario.Ativo = dados.Ativo;
        if (usuario.Id == 0)
        {
            db.Usuarios.Add(usuario);
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    public async Task<UsuarioLogado> TrocarPinAsync(int id, string pinAtual, string novoPin, string confirmacao)
    {
        if (!PinValido(novoPin) || novoPin != confirmacao || novoPin == pinAtual)
            throw new RegraNegocioException("Informe e confirme um novo PIN de 4 dígitos, diferente do atual.");
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id && u.Ativo);
        if (usuario is null || !ConferePin(usuario, pinAtual))
            throw new RegraNegocioException("PIN atual inválido.");
        if ((await db.Usuarios.Where(u => u.Id != id).ToListAsync()).Any(u => ConferePin(u, novoPin)))
            throw new RegraNegocioException("Já existe um usuário com esse PIN.");
        usuario.PinAcesso = ProtegerPin(usuario, novoPin);
        usuario.TrocaPinObrigatoria = false;
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
        return new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Perfil);
    }

    public async Task<bool> TemUsuariosAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Usuarios.AnyAsync();
    }

    internal static async Task ValidarOperacaoAsync(AppDbContext db, int usuarioId)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId && u.Ativo && !u.TrocaPinObrigatoria))
            throw new RegraNegocioException("Usuário inválido ou com troca de PIN pendente. Faça login e troque seu PIN.");
    }

    public async Task CriarGerenteInicialAsync(string nome, string pin)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > 100 || !PinValido(pin))
            throw new RegraNegocioException("Informe o nome do gerente e um PIN pessoal de 4 dígitos.");
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        if (await db.Usuarios.AnyAsync())
            throw new RegraNegocioException("O gerente inicial já foi configurado.");
        var usuario = new Usuario { Nome = nome.Trim(), Perfil = PerfilUsuario.Gerente, TrocaPinObrigatoria = false };
        usuario.PinAcesso = ProtegerPin(usuario, pin);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    public static bool PinValido(string? pin) => pin is { Length: 4 } && pin.All(char.IsAsciiDigit);
}
