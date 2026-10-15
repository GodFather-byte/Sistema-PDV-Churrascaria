using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Pagamento
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public int? ComandaId { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public MeioPagamento MeioPagamento { get; set; }
    public decimal ValorPago { get; set; }
    public decimal ValorTroco { get; set; }
    public decimal ValorTaxaServicoIncluso { get; set; }
    public Mesa Mesa { get; set; } = null!;
    public Comanda? Comanda { get; set; }
}
