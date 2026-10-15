namespace ChurrascariaPDV.Domain.Entities;

public class CategoriaProduto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public ICollection<Produto> Produtos { get; set; } = new List<Produto>();
}
