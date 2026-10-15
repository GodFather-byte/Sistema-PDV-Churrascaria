using ChurrascariaPDV.Components;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CardapioService>();
builder.Services.AddScoped<EstoqueService>();
builder.Services.AddScoped<MesaService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<PagamentoService>();
builder.Services.AddScoped<CaixaService>();
builder.Services.AddScoped<RelatorioService>();
builder.Services.AddScoped<PerfilEstabelecimentoService>();
builder.Services.AddScoped<SessaoUsuario>();

var app = builder.Build();

await BancoDeDados.PrepararAsync(app.Services, app.Logger);

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
