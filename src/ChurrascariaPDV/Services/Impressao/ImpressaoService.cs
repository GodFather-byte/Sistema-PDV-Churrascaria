using System.Net.Sockets;
using ChurrascariaPDV.Domain.Entities;
using Microsoft.Extensions.Options;

namespace ChurrascariaPDV.Services.Impressao;

/// <summary>Envia tickets ESC/POS às impressoras térmicas de rede (porta 9100) de cada setor.</summary>
public class ImpressaoService(
    IOptionsMonitor<ImpressaoOptions> options,
    HistoricoImpressoes historico,
    ILogger<ImpressaoService> logger)
{
    public ImpressaoOptions Configuracao => options.CurrentValue;

    public DocumentoEscPos NovoDocumento(PerfilEstabelecimento perfil) => new(perfil.LarguraTicket);

    /// <summary>Retorna verdadeiro quando o ticket foi entregue (ou simulado). Nunca lança exceção.</summary>
    public async Task<bool> ImprimirAsync(SetorImpressao setor, string titulo, DocumentoEscPos documento, CancellationToken cancellationToken = default)
    {
        var config = Configuracao;
        var texto = documento.ParaTexto();
        var destino = setor.Ip.Length > 0 ? $"{setor.Ip}:{setor.Porta}" : "(não configurada)";

        if (config.ModoSimulacao || !setor.Habilitada)
        {
            logger.LogInformation("[SIMULAÇÃO] Impressora {Setor} ({Destino}) - {Titulo}{NovaLinha}{Ticket}",
                setor.Nome, destino, titulo, Environment.NewLine, texto);
            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, true, true, null, texto));
            return true;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(setor.Ip))
            {
                throw new IOException("IP da impressora não configurado.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(config.TimeoutMs);

            using var cliente = new TcpClient();
            await cliente.ConnectAsync(setor.Ip, setor.Porta, timeout.Token);
            await using var stream = cliente.GetStream();
            await stream.WriteAsync(documento.ParaBytes(), timeout.Token);
            await stream.FlushAsync(timeout.Token);

            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, false, true, null, texto));
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            var erro = ex is OperationCanceledException ? "Tempo esgotado ao conectar na impressora" : ex.Message;
            logger.LogError(ex, "Falha ao imprimir {Titulo} no setor {Setor} ({Destino})", titulo, setor.Nome, destino);
            historico.Adicionar(new RegistroImpressao(DateTime.Now, setor, titulo, destino, false, false, erro, texto));
            return false;
        }
    }
}
