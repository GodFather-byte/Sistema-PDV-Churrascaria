using ChurrascariaPDV.Components;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Licenciamento;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

var instalar = args.Contains("--instalar", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--instalar").ToArray());

var porta = builder.Configuration.GetValue("Servidor:Porta", 5000);
builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=churrascaria.db"));

builder.Services.Configure<ImpressaoOptions>(builder.Configuration.GetSection(ImpressaoOptions.Secao));
builder.Services.AddSingleton<HistoricoImpressoes>();
builder.Services.AddSingleton<ImpressaoService>();
builder.Services.AddSingleton<NotificadorMesas>();
builder.Services.AddSingleton<RedeLocalService>();
builder.Services.AddSingleton<LimiteTentativasPin>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ProtecaoPin>();
builder.Services.AddSingleton(sp => new LicencaService(sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
builder.Services.AddHostedService<BackupBancoService>();

builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CardapioService>();
builder.Services.AddScoped<EstoqueService>();
builder.Services.AddScoped<MesaService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<PagamentoService>();
builder.Services.AddScoped<CaixaService>();
builder.Services.AddScoped<RelatorioService>();
builder.Services.AddScoped<PerfilEstabelecimentoService>();
builder.Services.AddScoped<SetorImpressaoService>();
builder.Services.AddScoped<SessaoUsuario>();

var app = builder.Build();

var arquivoBanco = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=churrascaria.db").DataSource;
var pendenciaInstalacao = $"{arquivoBanco}.instalacao-pendente";
if (instalar)
{
    if (string.IsNullOrWhiteSpace(arquivoBanco) || arquivoBanco == ":memory:" || File.Exists(arquivoBanco))
        throw new InvalidOperationException("A instalação exige um arquivo de banco novo. Nenhum banco existente será sobrescrito.");
    await using (var reserva = new FileStream(pendenciaInstalacao, FileMode.CreateNew, FileAccess.Write)) { }
}
else if (File.Exists(pendenciaInstalacao))
{
    throw new InvalidOperationException("A instalação anterior não foi concluída. Não inicie este banco; consulte o erro do assistente e use uma pasta nova.");
}

await BancoDeDados.PrepararAsync(app.Services, app.Logger);
if (instalar)
{
    await using var scope = app.Services.CreateAsyncScope();
    var usuarios = scope.ServiceProvider.GetRequiredService<UsuarioService>();
    Console.WriteLine("Informe o PIN pessoal do gerente pela entrada padrão (não será exibido nem salvo na configuração).");
    // O PowerShell pode enviar a marca de codificação (BOM) antes do texto redirecionado.
    var pin = (await Console.In.ReadLineAsync())?.Trim('﻿', ' ', '\t')
        ?? throw new InvalidOperationException("Nenhum PIN informado. Instalação incompleta.");
    await usuarios.CriarGerenteInicialAsync(builder.Configuration["Instalacao:NomeGerente"] ?? "Dono", pin);
    File.Delete(pendenciaInstalacao);
    Console.WriteLine("Instalação concluída. Perfil, exemplos e gerente configurados.");
    await app.DisposeAsync();
    return;
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SetorImpressaoService>()
        .ImportarConfiguracaoLegadaAsync(scope.ServiceProvider.GetRequiredService<IOptions<ImpressaoOptions>>().Value);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<MesasHub>("/hubs/mesas");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var rede = app.Services.GetRequiredService<RedeLocalService>();
app.Logger.LogInformation("Caixa:   http://localhost:{Porta}/caixa", porta);
app.Logger.LogInformation("Garçons: {Url}", rede.ObterUrlGarcom());

app.Run();
