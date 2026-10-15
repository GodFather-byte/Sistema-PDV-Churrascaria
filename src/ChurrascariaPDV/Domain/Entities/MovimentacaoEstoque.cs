using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int? ItemPedidoId { get; set; }
    public int? UsuarioId { get; set; }
    public TipoMovimentacaoEstoque Tipo { get; set; }
    public decimal Quantidade { get; set; }
    public decimal SaldoApos { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public string? Observacao { get; set; }
    public Produto Produto { get; set; } = null!;
    public ItemPedido? ItemPedido { get; set; }
    public Usuario? Usuario { get; set; }
}
