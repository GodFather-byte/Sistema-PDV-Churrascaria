using System.Collections.Concurrent;

namespace ChurrascariaPDV.Services;

/// <summary>
/// Conta as falhas de PIN por aparelho (IP) e bloqueia novas tentativas por um tempo crescente,
/// impedindo que alguém na rede do restaurante teste todos os PINs de 4 dígitos.
/// </summary>
public class LimiteTentativasPin(TimeProvider relogio)
{
    public const int TentativasLivres = 5;
    private static readonly TimeSpan BloqueioInicial = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BloqueioMaximo = TimeSpan.FromMinutes(15);

    private sealed record Estado(int Falhas, DateTimeOffset BloqueadoAte);

    private readonly ConcurrentDictionary<string, Estado> _origens = new();

    public LimiteTentativasPin() : this(TimeProvider.System) { }

    public void GarantirLiberado(string origem)
    {
        if (_origens.TryGetValue(origem, out var estado) && estado.BloqueadoAte > relogio.GetUtcNow())
        {
            var segundos = (int)Math.Ceiling((estado.BloqueadoAte - relogio.GetUtcNow()).TotalSeconds);
            throw new RegraNegocioException($"Muitas tentativas de PIN incorretas. Aguarde {segundos} s e tente novamente.");
        }
    }

    public void RegistrarFalha(string origem) => _origens.AddOrUpdate(origem,
        _ => new Estado(1, DateTimeOffset.MinValue),
        (_, atual) =>
        {
            var falhas = atual.Falhas + 1;
            if (falhas < TentativasLivres) return atual with { Falhas = falhas };
            var fator = Math.Pow(2, Math.Min(falhas - TentativasLivres, 10));
            var bloqueio = TimeSpan.FromTicks((long)Math.Min(BloqueioInicial.Ticks * fator, BloqueioMaximo.Ticks));
            return new Estado(falhas, relogio.GetUtcNow() + bloqueio);
        });

    public void RegistrarSucesso(string origem) => _origens.TryRemove(origem, out _);
}

/// <summary>Aplica o <see cref="LimiteTentativasPin"/> ao aparelho da conexão atual.</summary>
public class ProtecaoPin(LimiteTentativasPin limite, IHttpContextAccessor contexto)
{
    private readonly string _origem = contexto.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";

    public void GarantirLiberado() => limite.GarantirLiberado(_origem);
    public void RegistrarFalha() => limite.RegistrarFalha(_origem);
    public void RegistrarSucesso() => limite.RegistrarSucesso(_origem);
}
