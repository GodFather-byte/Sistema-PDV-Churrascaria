using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Mesa
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public int CapacidadeLugares { get; set; }
    public StatusMesa Status { get; set; } = StatusMesa.Livre;
    public DateTime? DataHoraAbertura { get; set; }
    public int QuantidadePessoas { get; set; }
    public decimal ValorTotalAtual { get; set; }
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();
}
