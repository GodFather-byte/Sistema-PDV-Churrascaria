namespace ChurrascariaPDV.Services.Impressao;

public class ImpressaoOptions
{
    public const string Secao = "Impressao";

    /// <summary>Quando verdadeiro nenhum byte é enviado à rede: o ticket vai para o log e para a tela /impressoes.</summary>
    public bool ModoSimulacao { get; set; } = true;
    public int TimeoutMs { get; set; } = 3000;
    // Lida apenas na importacao unica da configuracao anterior.
    public Dictionary<string, ImpressoraConfig> Impressoras { get; set; } = new();
}

public class ImpressoraConfig
{
    public string Ip { get; set; } = string.Empty;
    public int Porta { get; set; } = 9100;
    public bool Habilitada { get; set; } = true;
}
