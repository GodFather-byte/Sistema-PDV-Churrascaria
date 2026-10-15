namespace ChurrascariaPDV.Domain.Entities;

public class OpcaoModificador
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int GrupoModificadorId { get; set; }
    public GrupoModificador GrupoModificador { get; set; } = null!;
}
