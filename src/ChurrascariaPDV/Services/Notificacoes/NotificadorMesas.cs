using Microsoft.AspNetCore.SignalR;

namespace ChurrascariaPDV.Services.Notificacoes;

/// <summary>
/// Propaga alterações de mesas para todas as telas abertas (caixa e celulares dos garçons).
/// Componentes Blazor assinam o evento em memória; clientes externos recebem via hub SignalR.
/// </summary>
public class NotificadorMesas(IHubContext<MesasHub> hubContext, ILogger<NotificadorMesas> logger)
{
    public event Func<int?, Task>? MesasAlteradas;

    public async Task NotificarAsync(params int[] mesaIds)
    {
        var handlers = MesasAlteradas?.GetInvocationList().Cast<Func<int?, Task>>().ToArray() ?? [];
        var ids = mesaIds.Length == 0 ? [(int?)null] : mesaIds.Select(id => (int?)id).ToArray();

        foreach (var id in ids)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await handler(id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao notificar uma tela sobre a mesa {MesaId}", id);
                }
            }

            await hubContext.Clients.All.SendAsync("MesaAtualizada", id);
        }
    }
}

public class MesasHub : Hub;
