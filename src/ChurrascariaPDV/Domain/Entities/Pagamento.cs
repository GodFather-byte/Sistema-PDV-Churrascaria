using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Pagamento
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public int? ComandaId { get; set; }
    public int? TurnoCaixaId { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public MeioPagamento MeioPagamento { get; set; }
    /// <summary>Valor abatido do saldo devedor (sem o troco).</summary>
    public decimal ValorPago { get; set; }
    public decimal ValorTroco { get; set; }
    public decimal ValorTaxaServicoIncluso { get; set; }
    public bool Estornado { get; set; }
    public DateTime? DataHoraEstorno { get; set; }
    public string? MotivoEstorno { get; set; }
    public int? UsuarioGerenteEstornoId { get; set; }
    public Usuario? UsuarioGerenteEstorno { get; set; }
    public Mesa Mesa { get; set; } = null!;
    public Comanda? Comanda { get; set; }
    public TurnoCaixa? TurnoCaixa { get; set; }
    public Usuario? Usuario { get; set; }

    public decimal ValorRecebido => ValorPago + ValorTroco;
}
