using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Comanda
{
    public int Id { get; set; }
    public int CodigoNumero { get; set; }
    public int MesaId { get; set; }
    public string? NomeCliente { get; set; }
    public StatusComanda Status { get; set; } = StatusComanda.Aberta;
    public bool CobrarTaxaServico { get; set; } = true;
    public Mesa Mesa { get; set; } = null!;
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();
}
