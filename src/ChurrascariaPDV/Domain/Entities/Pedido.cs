using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Pedido
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public int ComandaId { get; set; }
    public int UsuarioGarcomId { get; set; }
    public DateTime DataHoraLancamento { get; set; } = DateTime.Now;
    public bool ImpressoNosSetores { get; set; }
    public StatusPedido Status { get; set; } = StatusPedido.Aberto;
    public Mesa Mesa { get; set; } = null!;
    public Comanda Comanda { get; set; } = null!;
    public Usuario UsuarioGarcom { get; set; } = null!;
    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
}
