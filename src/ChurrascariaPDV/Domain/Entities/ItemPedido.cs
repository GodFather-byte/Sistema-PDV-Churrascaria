namespace ChurrascariaPDV.Domain.Entities;

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProdutoId { get; set; }
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitarioHistorico { get; set; }
    public string? ObservacoesModificadores { get; set; }
    public string? SetorImpressaoId { get; set; }
    public SetorImpressao? SetorImpressao { get; set; }
    public bool DestacarObservacoes { get; set; }
    public bool Cancelado { get; set; }
    public string? MotivoCancelamento { get; set; }
    public bool AutorizadoPorGerente { get; set; }
    public int? UsuarioGerenteAutorizacaoId { get; set; }
    public DateTime? DataHoraCancelamento { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public Produto Produto { get; set; } = null!;
    public Usuario? UsuarioGerenteAutorizacao { get; set; }

    public decimal ValorTotal => Cancelado ? 0 : Math.Round(Quantidade * PrecoUnitarioHistorico, 2, MidpointRounding.AwayFromZero);
}
