namespace ChurrascariaPDV.Domain.Entities;

public class TurnoCaixa
{
    public int Id { get; set; }
    public DateTime DataHoraAbertura { get; set; } = DateTime.Now;
    public DateTime? DataHoraFechamento { get; set; }
    public int? UsuarioAberturaId { get; set; }
    public int? UsuarioFechamentoId { get; set; }
    public decimal FundoTroco { get; set; }
    /// <summary>Dinheiro contado na gaveta no fechamento.</summary>
    public decimal? ValorFechamento { get; set; }
    /// <summary>Dinheiro que o sistema esperava na gaveta no fechamento.</summary>
    public decimal? ValorEsperadoDinheiro { get; set; }
    public string? Observacao { get; set; }
    public Usuario? UsuarioAbertura { get; set; }
    public Usuario? UsuarioFechamento { get; set; }
    public ICollection<MovimentacaoCaixa> Movimentacoes { get; set; } = new List<MovimentacaoCaixa>();
    public ICollection<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();

    public bool Aberto => DataHoraFechamento is null;
}
