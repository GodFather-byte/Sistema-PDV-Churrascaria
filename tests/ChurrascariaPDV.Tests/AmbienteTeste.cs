using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChurrascariaPDV.Tests;

/// <summary>Monta os serviços reais sobre um SQLite em memória, com impressão em modo simulação.</summary>
public sealed class AmbienteTeste : IAsyncDisposable
{
    public const int Gerente = 1;
    public const int Caixa = 2;
    public const int Garcom = 3;
    public const string PinGerente = "1234";

    public const int RodizioAdulto = 1;
    public const int Picanha = 3;
    public const int PolentaFrita = 5;
    public const int Chopp = 7;
    public const int SucoJarra = 9;

    public const int AoPonto = 2;
    public const int ComGeloLimao = 4;

    private readonly SqliteConnection _conexao;
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    private AmbienteTeste(SqliteConnection conexao, ServiceProvider provider)
    {
        _conexao = conexao;
        _provider = provider;
        _scope = provider.CreateAsyncScope();
    }

    public static async Task<AmbienteTeste> CriarAsync()
    {
        var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(conexao));
        services.Configure<ImpressaoOptions>(o => o.ModoSimulacao = true);
        services.AddSingleton<HistoricoImpressoes>();
        services.AddSingleton<ImpressaoService>();
        services.AddSingleton<NotificadorMesas>();
        services.AddScoped<UsuarioService>();
        services.AddScoped<MesaService>();
        services.AddScoped<PedidoService>();
        services.AddScoped<PagamentoService>();
        services.AddScoped<CaixaService>();
        services.AddScoped<RelatorioService>();

        var provider = services.BuildServiceProvider();
        await using (var db = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        return new AmbienteTeste(conexao, provider);
    }

    public T Servico<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public MesaService Mesas => Servico<MesaService>();
    public PedidoService Pedidos => Servico<PedidoService>();
    public PagamentoService Pagamentos => Servico<PagamentoService>();
    public CaixaService Caixa_ => Servico<CaixaService>();
    public HistoricoImpressoes Impressoes => Servico<HistoricoImpressoes>();

    public AppDbContext NovoContexto() => _scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    public Task<ResultadoPedido> LancarAsync(int mesaId, params ItemNovoPedido[] itens) =>
        Pedidos.LancarPedidoAsync(new NovoPedido(mesaId, null, Garcom, itens));

    public static ItemNovoPedido Item(int produtoId, decimal quantidade = 1, params int[] opcoes) =>
        new(produtoId, quantidade, opcoes);

    public Task AbrirCaixaAsync(decimal fundo = 100) => Caixa_.AbrirTurnoAsync(fundo, Caixa);

    public Task<ResultadoPagamento> PagarAsync(int mesaId, decimal valor, MeioPagamento meio = MeioPagamento.Pix, int? comandaId = null) =>
        Pagamentos.RegistrarAsync(new NovoPagamento(mesaId, comandaId, meio, valor, Caixa));

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        await _conexao.DisposeAsync();
    }
}
