using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class MovimentacaoCaixa
{
    public int Id { get; set; }
    public int TurnoCaixaId { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public TipoMovimentacaoCaixa Tipo { get; set; }
    public decimal Valor { get; set; }
    public string? Observacao { get; set; }
    public TurnoCaixa TurnoCaixa { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}
