using System.Net.Http.Json;
using Licenciamento.Comum;

namespace ChurrascariaPDV.Services.Licenciamento;

/// <summary>
/// Confere a licença no servidor do fornecedor a cada <see cref="Intervalo"/>, quando há internet:
/// recebe a licença renovada ou o bloqueio. Sem conexão nada muda e o arquivo offline continua valendo.
/// </summary>
public class VerificacaoOnlineService(LicencaService licencas, IHttpClientFactory http, IConfiguration configuration,
    ILogger<VerificacaoOnlineService> logger) : BackgroundService
{
    /// <summary>Endereço do servidor de licenças. Pode ser trocado em <c>Licenca:Servidor</c> no appsettings.json.</summary>
    public const string ServidorPadrao = "";

    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim _emAndamento = new(1, 1);

    public string? Servidor => (configuration["Licenca:Servidor"] ?? ServidorPadrao).Trim().TrimEnd('/') is { Length: > 0 } url ? url : null;

    public DateTimeOffset? UltimaTentativa { get; private set; }
    public DateTimeOffset? UltimoSucesso { get; private set; }
    public string? UltimoResultado { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken cancelamento)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), cancelamento);
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await VerificarAgoraAsync(cancelamento);
        } while (await timer.WaitForNextTickAsync(cancelamento));
    }

    /// <summary>Faz a conferência imediatamente e devolve uma mensagem para exibir ao gerente.</summary>
    public async Task<string> VerificarAgoraAsync(CancellationToken cancelamento = default)
    {
        if (Servidor is not { } servidor)
            return UltimoResultado = "Conferência online desativada: servidor de licenças não configurado.";

        await _emAndamento.WaitAsync(cancelamento);
        try
        {
            UltimaTentativa = DateTimeOffset.Now;
            var versao = typeof(VerificacaoOnlineService).Assembly.GetName().Version?.ToString(3);
            using var cliente = http.CreateClient(nameof(VerificacaoOnlineService));
            cliente.Timeout = TimeSpan.FromSeconds(20);
            using var resposta = await cliente.PostAsJsonAsync($"{servidor}/api/v1/verificar",
                licencas.CriarPedidoVerificacao(versao), cancelamento);
            if (!resposta.IsSuccessStatusCode)
                return UltimoResultado = $"Servidor de licenças respondeu {(int)resposta.StatusCode}. Nada foi alterado.";

            var resultado = await licencas.AplicarRespostaOnlineAsync(await resposta.Content.ReadAsStringAsync(cancelamento));
            UltimoSucesso = DateTimeOffset.Now;
            return UltimoResultado = resultado.Status switch
            {
                StatusVerificacao.Ativa => "Licença conferida online: em dia.",
                StatusVerificacao.Bloqueada => $"Licença bloqueada pelo fornecedor: {resultado.Mensagem}",
                _ => "Nenhuma licença cadastrada no servidor para este computador."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or RegraNegocioException or IOException
                                       && !cancelamento.IsCancellationRequested)
        {
            logger.LogInformation("Conferência online da licença não concluída: {Erro}", ex.Message);
            return UltimoResultado = ex is RegraNegocioException
                ? ex.Message
                : "Sem conexão com o servidor de licenças. A licença do arquivo continua valendo.";
        }
        finally
        {
            _emAndamento.Release();
        }
    }
}
