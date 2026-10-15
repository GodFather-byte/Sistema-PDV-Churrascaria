using System.Net.Sockets;
using ChurrascariaPDV.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ChurrascariaPDV.Services.Impressao;

/// <summary>Envia tickets ESC/POS às impressoras térmicas de rede (porta 9100) de cada setor.</summary>
public class ImpressaoService(
    IOptionsMonitor<ImpressaoOptions> options,
    HistoricoImpressoes historico,
    ILogger<ImpressaoService> logger)
{
    public ImpressaoOptions Configuracao => options.CurrentValue;

    public DocumentoEscPos NovoDocumento() => new(Configuracao.LarguraColunas);

    /// <summary>Retorna verdadeiro quando o ticket foi entregue (ou simulado). Nunca lança exceção.</summary>
    public async Task<bool> ImprimirAsync(SetorImpressao setor, string titulo, DocumentoEscPos documento, CancellationToken cancellationToken = default)
    {
        if (setor == SetorImpressao.Nenhum)
        {
            return true;
        }

        var config = Configuracao;
        var texto = documento.ParaTexto();
        config.Impressoras.TryGetValue(setor, out var impressora);
        var destino = impressora is { Ip.Length: > 0 } ? $"{impressora.Ip}:{impressora.Porta}" : "(não configurada)";

        if (config.ModoSimulacao || impressora is null || !impressora.Habilitada || string.IsNullOrWhiteSpace(impressora.Ip))
        {
            logger.LogInformation("[SIMULAÇÃO] Impressora {Setor} ({Destino}) - {Titulo}{NovaLinha}{Ticket}",
                setor, destino, titulo, Environment.NewLine, texto);
            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, true, true, null, texto));
            return true;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(config.TimeoutMs);

            using var cliente = new TcpClient();
            await cliente.ConnectAsync(impressora.Ip, impressora.Porta, timeout.Token);
            await using var stream = cliente.GetStream();
            await stream.WriteAsync(documento.ParaBytes(), timeout.Token);
            await stream.FlushAsync(timeout.Token);

            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, false, true, null, texto));
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            var erro = ex is OperationCanceledException ? "Tempo esgotado ao conectar na impressora" : ex.Message;
            logger.LogError(ex, "Falha ao imprimir {Titulo} no setor {Setor} ({Destino})", titulo, setor, destino);
            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, false, false, erro, texto));
            return false;
        }
    }
}
