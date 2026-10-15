namespace ChurrascariaPDV.Domain.Entities;

public class GrupoModificador
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Obrigatorio { get; set; }
    public bool SelecaoUnica { get; set; }
    public string? SetorImpressaoId { get; set; }
    public SetorImpressao? SetorImpressao { get; set; }
    public bool DestacarObservacoes { get; set; }
    public bool SugerePessoas { get; set; }
    public bool Sabores { get; set; }
    public ICollection<OpcaoModificador> Opcoes { get; set; } = new List<OpcaoModificador>();
    public ICollection<GrupoModificadorProduto> Produtos { get; set; } = new List<GrupoModificadorProduto>();
}
