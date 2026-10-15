using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class Comanda
{
    /// <summary>Código reservado para a conta geral da mesa.</summary>
    public const int CodigoContaGeral = 0;

    public int Id { get; set; }
    public int CodigoNumero { get; set; }
    public int MesaId { get; set; }
    public string? NomeCliente { get; set; }
    public StatusComanda Status { get; set; } = StatusComanda.Aberta;
    public bool CobrarTaxaServico { get; set; } = true;
    public DateTime DataHoraAbertura { get; set; } = DateTime.Now;
    public DateTime? DataHoraFechamento { get; set; }
    public Mesa Mesa { get; set; } = null!;
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();

    public bool ContaGeral => CodigoNumero == CodigoContaGeral;

    public string Descricao => ContaGeral
        ? "Conta Geral"
        : string.IsNullOrWhiteSpace(NomeCliente) ? $"Comanda {CodigoNumero}" : $"Comanda {CodigoNumero} · {NomeCliente}";
}
