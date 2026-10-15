using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Services.Impressao;

public class ImpressaoOptions
{
    public const string Secao = "Impressao";

    /// <summary>Quando verdadeiro nenhum byte é enviado à rede: o ticket vai para o log e para a tela /impressoes.</summary>
    public bool ModoSimulacao { get; set; } = true;
    public int TimeoutMs { get; set; } = 3000;
    public int LarguraColunas { get; set; } = 48;
    public string NomeEstabelecimento { get; set; } = "CHURRASCARIA";
    public Dictionary<SetorImpressao, ImpressoraConfig> Impressoras { get; set; } = new();
}

public class ImpressoraConfig
{
    public string Ip { get; set; } = string.Empty;
    public int Porta { get; set; } = 9100;
    public bool Habilitada { get; set; } = true;
}
