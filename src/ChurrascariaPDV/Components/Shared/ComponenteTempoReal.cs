using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.AspNetCore.Components;

namespace ChurrascariaPDV.Components.Shared;

/// <summary>
/// Base das telas que precisam refletir na hora o que outros terminais fazem nas mesas.
/// Também atualiza a tela periodicamente para o tempo de permanência das mesas.
/// </summary>
public abstract class ComponenteTempoReal : ComponentBase, IDisposable
{
    [Inject] protected NotificadorMesas Notificador { get; set; } = null!;

    private Timer? _relogio;

    protected virtual TimeSpan IntervaloRelogio => TimeSpan.FromSeconds(30);

    protected override void OnInitialized()
    {
        Notificador.MesasAlteradas += AoNotificar;
        _relogio = new Timer(_ => InvokeAsync(StateHasChanged), null, IntervaloRelogio, IntervaloRelogio);
    }

    /// <summary>Chamado (no contexto do componente) quando alguma mesa muda. Null = mudança geral.</summary>
    protected abstract Task AoAlterarMesasAsync(int? mesaId);

    private Task AoNotificar(int? mesaId)
    {
        _ = InvokeAsync(async () =>
        {
            try
            {
                await AoAlterarMesasAsync(mesaId);
                StateHasChanged();
            }
            catch (ObjectDisposedException)
            {
                // Tela fechada no meio da atualização.
            }
        });
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        Notificador.MesasAlteradas -= AoNotificar;
        _relogio?.Dispose();
        GC.SuppressFinalize(this);
    }
}
