using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ChurrascariaPDV.Services;

/// <summary>
/// Login por PIN de cada tela (garçom ou caixa). Fica guardado no navegador, criptografado,
/// para o garçom não precisar digitar o PIN de novo se o celular recarregar a página.
/// </summary>
public class SessaoUsuario(ProtectedLocalStorage storage, UsuarioService usuarios, ILogger<SessaoUsuario> logger)
{
    private static readonly TimeSpan Validade = TimeSpan.FromHours(14);

    private record SessaoSalva(int UsuarioId, DateTime ExpiraEm);

    public UsuarioLogado? Usuario { get; private set; }

    public async Task<UsuarioLogado?> RestaurarAsync(string area)
    {
        try
        {
            var salvo = await storage.GetAsync<SessaoSalva>(Chave(area));
            if (salvo is { Success: true, Value: { } sessao } && sessao.ExpiraEm > DateTime.Now)
            {
                Usuario = await usuarios.ObterAsync(sessao.UsuarioId);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException or System.Text.Json.JsonException)
        {
            logger.LogDebug(ex, "Sessão salva ignorada");
            await SairAsync(area);
        }

        return Usuario;
    }

    public async Task EntrarAsync(string area, UsuarioLogado usuario)
    {
        Usuario = usuario;
        await storage.SetAsync(Chave(area), new SessaoSalva(usuario.Id, DateTime.Now.Add(Validade)));
    }

    public async Task SairAsync(string area)
    {
        Usuario = null;
        try
        {
            await storage.DeleteAsync(Chave(area));
        }
        catch (InvalidOperationException)
        {
            // Durante a pré-renderização não há acesso ao navegador; nada a remover.
        }
    }

    private static string Chave(string area) => $"churrascaria-pdv-sessao-{area}";
}
