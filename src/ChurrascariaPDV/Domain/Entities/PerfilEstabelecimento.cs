using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Domain.Entities;

public class PerfilEstabelecimento
{
    public const int IdPadrao = 1;

    public int Id { get; set; } = IdPadrao;
    public TipoEstabelecimento Tipo { get; set; } = TipoEstabelecimento.Churrascaria;
    public string Nome { get; set; } = "CHURRASCARIA";
    public string Endereco { get; set; } = string.Empty;
    public decimal PercentualTaxaServico { get; set; } = 10m;
    public bool TaxaServicoOpcionalPorComanda { get; set; } = true;
    public int LarguraTicket { get; set; } = 48;
}
