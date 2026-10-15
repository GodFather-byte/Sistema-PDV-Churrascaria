namespace ChurrascariaPDV.Domain.Entities;

public class TurnoCaixa
{
    public int Id { get; set; }
    public DateTime DataHoraAbertura { get; set; } = DateTime.Now;
    public DateTime? DataHoraFechamento { get; set; }
    public decimal FundoTroco { get; set; }
    public decimal? ValorFechamento { get; set; }
    public ICollection<MovimentacaoCaixa> Movimentacoes { get; set; } = new List<MovimentacaoCaixa>();
}
