namespace ChurrascariaPDV.Domain.Entities;

public class GrupoModificadorProduto
{
    public int ProdutoId { get; set; }
    public int GrupoModificadorId { get; set; }
    public Produto Produto { get; set; } = null!;
    public GrupoModificador GrupoModificador { get; set; } = null!;
}
