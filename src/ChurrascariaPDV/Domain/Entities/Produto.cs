using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public RegraPrecoSabores RegraPrecoSabores { get; set; }
    public int MaximoSabores { get; set; } = 2;
    public int CategoriaId { get; set; }
    public TipoItem TipoItem { get; set; } = TipoItem.Produto;
    public string? SetorImpressaoId { get; set; }
    public SetorImpressao? SetorImpressao { get; set; }
    public bool Ativo { get; set; } = true;
    public bool ControlaEstoque { get; set; }
    public bool QuantidadeFracionada { get; set; }
    public decimal QuantidadeEstoque { get; set; }
    public CategoriaProduto Categoria { get; set; } = null!;
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
    public ICollection<GrupoModificadorProduto> GruposModificadores { get; set; } = new List<GrupoModificadorProduto>();
    public ICollection<MovimentacaoEstoque> MovimentacoesEstoque { get; set; } = new List<MovimentacaoEstoque>();
}
