namespace ChurrascariaPDV.Domain.Entities;

public class GrupoModificador
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Obrigatorio { get; set; }
    public ICollection<OpcaoModificador> Opcoes { get; set; } = new List<OpcaoModificador>();
    public ICollection<GrupoModificadorProduto> Produtos { get; set; } = new List<GrupoModificadorProduto>();
}
