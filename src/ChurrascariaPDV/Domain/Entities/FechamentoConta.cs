namespace ChurrascariaPDV.Domain.Entities;

public class FechamentoConta
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public int? ComandaId { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public decimal Subtotal { get; set; }
    public decimal ValorTaxaServico { get; set; }
    public decimal Total { get; set; }
    public int QuantidadePessoas { get; set; }
    public Mesa Mesa { get; set; } = null!;
    public Comanda? Comanda { get; set; }
}
