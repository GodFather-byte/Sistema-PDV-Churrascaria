using System.Security.Cryptography;
using ChurrascariaPDV.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChurrascariaPDV.Tests;

public class SessaoUsuarioTests
{
    [Fact]
    public async Task RestaurarAreaSemSessaoNaoReutilizaUsuarioDaAreaAnterior()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var armazenamento = new ProtectedLocalStorage(
            new ArmazenamentoJavaScriptVazio(),
            DataProtectionProvider.Create("ChurrascariaPDV.Tests"));
        var sessao = new SessaoUsuario(
            armazenamento,
            ambiente.Servico<UsuarioService>(),
            NullLogger<SessaoUsuario>.Instance);

        await sessao.EntrarAsync("gerencia", new UsuarioLogado(1, "Gerente", Domain.Enums.PerfilUsuario.Gerente));
        var restaurado = await sessao.RestaurarAsync("caixa");

        Assert.Null(restaurado);
        Assert.Null(sessao.Usuario);
    }

    private sealed class ArmazenamentoJavaScriptVazio : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }
}
