namespace ChurrascariaPDV.Domain.Entities;

public class SetorImpressao
{
    public const string BarCopa = "BarCopa";
    public const string Cozinha = "Cozinha";
    public const string Churrasqueira = "Churrasqueira";
    public const string Caixa = "Caixa";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Porta { get; set; } = 9100;
    public bool Habilitada { get; set; } = true;
    public bool RecebeContas { get; set; }
    public bool ItensEmDestaque { get; set; } = true;
    public bool ObservacoesEmDestaque { get; set; }
    public bool ConfiguracaoLegadaImportada { get; set; }
    public int Ordem { get; set; }
}
