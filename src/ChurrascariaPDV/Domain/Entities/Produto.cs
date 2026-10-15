using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int CategoriaId { get; set; }
    public TipoItem TipoItem { get; set; }
    public SetorImpressao SetorImpressao { get; set; }
    public bool Ativo { get; set; } = true;
    public CategoriaProduto Categoria { get; set; } = null!;
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
    public ICollection<GrupoModificadorProduto> GruposModificadores { get; set; } = new List<GrupoModificadorProduto>();
}
