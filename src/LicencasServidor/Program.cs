using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using LicencasServidor.Components;
using LicencasServidor.Data;
using LicencasServidor.Services;
using Licenciamento.Comum;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// Gera o par de chaves do fornecedor (uma única vez):  dotnet LicencasServidor.dll gerar-chaves <pasta>
if (args is ["gerar-chaves", var pastaChaves])
{
    var arquivo = Path.Combine(Path.GetFullPath(pastaChaves), "chave-privada.pem");
    if (File.Exists(arquivo)) { Console.Error.WriteLine($"{arquivo} já existe. Nada foi sobrescrito."); return 1; }
    Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
    using var nova = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(arquivo, nova.ExportPkcs8PrivateKeyPem());
    Console.WriteLine($"Chave privada: {arquivo} (faça backup). Chave pública para ChavesLicenca:");
    Console.WriteLine(nova.ExportSubjectPublicKeyInfoPem());
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
var pastaDados = Path.GetFullPath(builder.Configuration["Dados:Pasta"] ?? "dados");
Directory.CreateDirectory(pastaDados);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDbContextFactory<LicencasDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Licencas") ?? $"Data Source={Path.Combine(pastaDados, "licencas.db")}"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => Emissor.DaConfiguracao(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<LimiteTentativas>();
builder.Services.AddScoped<LicencasService>();
builder.Services.AddScoped<AdministradoresService>();

// Mantém os logins válidos após reiniciar o servidor.
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaDados, "chaves-sessao")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/entrar";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// A hospedagem termina o HTTPS num proxy reverso e repassa o IP do cliente.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("verificacao", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.Services.GetRequiredService<Emissor>(); // Falha ao iniciar se a chave privada estiver ausente ou errada.
await using (var scope = app.Services.CreateAsyncScope())
{
    await using (var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<LicencasDbContext>>().CreateDbContextAsync())
    {
        await db.Database.MigrateAsync();
    }

    // Primeiro acesso: cria o administrador informado em Admin__Usuario / Admin__Senha, só se ainda não houver nenhum.
    var admins = scope.ServiceProvider.GetRequiredService<AdministradoresService>();
    if (!await admins.ExisteAlgumAsync())
    {
        if (app.Configuration["Admin:Usuario"] is { Length: > 0 } usuario && app.Configuration["Admin:Senha"] is { Length: > 0 } senha)
        {
            await admins.CriarAsync(usuario, senha);
            app.Logger.LogInformation("Administrador inicial {Usuario} criado. Remova Admin__Senha da configuração.", usuario);
        }
        else
        {
            app.Logger.LogWarning("Nenhum administrador cadastrado. Defina Admin__Usuario e Admin__Senha e reinicie.");
        }
    }
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro", createScopeForErrors: true);
}
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Conferência online dos caixas: recebe PedidoVerificacao (JSON) e devolve RespostaVerificacao assinada (texto).
app.MapPost("/api/v1/verificar", async (PedidoVerificacao pedido, LicencasService licencas, HttpContext contexto) =>
{
    try
    {
        return Results.Text(await licencas.VerificarAsync(pedido, contexto.Connection.RemoteIpAddress?.ToString()));
    }
    catch (RegraException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).RequireRateLimiting("verificacao").DisableAntiforgery();

app.MapPost("/conta/entrar", async (HttpContext contexto, AdministradoresService admins) =>
{
    var form = await contexto.Request.ReadFormAsync();
    try
    {
        var admin = await admins.AutenticarAsync(form["usuario"].ToString(), form["senha"].ToString(),
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
        if (admin is null) return Results.Redirect("/entrar?erro=1");
        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, admin.Usuario)], CookieAuthenticationDefaults.AuthenticationScheme);
        await contexto.SignInAsync(new ClaimsPrincipal(identidade));
        return Results.Redirect("/");
    }
    catch (RegraException)
    {
        return Results.Redirect("/entrar?erro=2");
    }
});

app.MapPost("/conta/sair", async (HttpContext contexto) =>
{
    await contexto.SignOutAsync();
    return Results.Redirect("/entrar");
});

app.MapGet("/saude", () => Results.Ok("ok"));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
return 0;
