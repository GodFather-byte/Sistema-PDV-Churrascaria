using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Licenciamento.Comum;

/// <summary>Dados de uma licença de assinatura, emitida para um único PC de caixa.</summary>
public record Licenca(string Id, string Cliente, string Maquina, DateOnly EmitidaEm, DateOnly ValidaAte);

/// <summary>Pedido que o caixa envia ao servidor de licenças. Sem Id, o servidor procura pela máquina.</summary>
public record PedidoVerificacao(string? LicencaId, string Maquina, string? Versao);

public enum StatusVerificacao { Ativa, Bloqueada, NaoEncontrada }

/// <summary>Resposta assinada do servidor. Em <see cref="StatusVerificacao.Ativa"/> traz o arquivo de licença atualizado.</summary>
public record RespostaVerificacao(StatusVerificacao Status, string Maquina, string? LicencaId, string? ArquivoLicenca,
    string? Mensagem, DateTimeOffset DataServidor);

/// <summary>Chave pública do emissor. A chave privada correspondente fica só com o fornecedor e no servidor de licenças.</summary>
public static class ChavesLicenca
{
    public const string Publica = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE9T/4XiVHvb0tYaTLpY1RrmmWtJWp
        orQVxKw1qcb3oM6ED6BER6KMMmmlovQZp13bCvKi9F91R3rp1xPZw2cfUQ==
        -----END PUBLIC KEY-----
        """;
}

/// <summary>
/// Documento assinado: <c>base64(json).base64(assinatura)</c>, com ECDSA P-256.
/// Usado no arquivo de licença e nas respostas do servidor.
/// </summary>
public static class FormatoLicenca
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Assinar<T>(T conteudo, ECDsa chavePrivada)
    {
        var dados = JsonSerializer.SerializeToUtf8Bytes(conteudo, Json);
        var assinatura = chavePrivada.SignData(dados, HashAlgorithmName.SHA256);
        return $"{Convert.ToBase64String(dados)}.{Convert.ToBase64String(assinatura)}";
    }

    /// <summary>Retorna o conteúdo se a assinatura conferir; caso contrário, null.</summary>
    public static T? Ler<T>(string texto, ECDsa chavePublica) where T : class
    {
        try
        {
            var partes = texto.Trim().Split('.');
            if (partes.Length != 2) return null;
            var dados = Convert.FromBase64String(partes[0]);
            var assinatura = Convert.FromBase64String(partes[1]);
            return chavePublica.VerifyData(dados, assinatura, HashAlgorithmName.SHA256)
                ? JsonSerializer.Deserialize<T>(dados, Json)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or NotSupportedException)
        {
            return null;
        }
    }

    public static ECDsa CarregarChave(string pem)
    {
        var chave = ECDsa.Create();
        chave.ImportFromPem(pem);
        return chave;
    }
}

/// <summary>Código curto e estável que identifica o PC do caixa (derivado do MachineGuid do Windows).</summary>
public static class IdentificacaoMaquina
{
    public static string Obter()
    {
        string? origem = null;
        if (OperatingSystem.IsWindows())
        {
            origem = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
        }
        return Formatar(origem ?? Environment.MachineName);
    }

    public static string Formatar(string origem)
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("PDV-MAQUINA|" + origem.Trim().ToUpperInvariant()));
        var codigo = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) codigo.Append('-');
            codigo.Append(alfabeto[hash[i] % alfabeto.Length]);
        }
        return codigo.ToString();
    }

    public static string Normalizar(string codigo) => codigo.Trim().ToUpperInvariant();
}
