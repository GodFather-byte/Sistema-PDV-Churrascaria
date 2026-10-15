using ChurrascariaPDV.Domain.Entities;

namespace ChurrascariaPDV.Services.Impressao;

public record RegistroImpressao(
    DateTime DataHora,
    SetorImpressao Setor,
    string Titulo,
    string Destino,
    bool Simulado,
    bool Sucesso,
    string? Erro,
    string Conteudo);

/// <summary>Guarda os últimos tickets em memória para conferência na tela /impressoes.</summary>
public class HistoricoImpressoes
{
    private const int Limite = 200;
    private readonly LinkedList<RegistroImpressao> _registros = new();
    private readonly object _lock = new();

    public event Action? Alterado;

    public void Adicionar(RegistroImpressao registro)
    {
        lock (_lock)
        {
            _registros.AddFirst(registro);
            while (_registros.Count > Limite)
            {
                _registros.RemoveLast();
            }
        }

        Alterado?.Invoke();
    }

    public IReadOnlyList<RegistroImpressao> Listar()
    {
        lock (_lock)
        {
            return _registros.ToList();
        }
    }

    public void Limpar()
    {
        lock (_lock)
        {
            _registros.Clear();
        }

        Alterado?.Invoke();
    }
}
