using System.Security.Cryptography;
using LicencasServidor.Data;
using Licenciamento.Comum;

namespace LicencasServidor.Services;

/// <summary>Assina licenças e respostas com a chave privada do fornecedor.</summary>
public sealed class Emissor : IDisposable
{
    private readonly ECDsa _chave;
    private readonly TimeProvider _relogio;

    public Emissor(ECDsa chavePrivada, TimeProvider relogio)
    {
        _chave = chavePrivada;
        _relogio = relogio;
    }

    /// <summary>
    /// Carrega a chave de <c>Licencas:ChavePrivada</c> (PEM) ou <c>Licencas:ArquivoChavePrivada</c> e confere
    /// se ela corresponde à chave pública embutida no PDV; caso contrário, o PDV recusaria tudo que for emitido.
    /// </summary>
    public static Emissor DaConfiguracao(IConfiguration configuration)
    {
        var pem = configuration["Licencas:ChavePrivada"];
        if (string.IsNullOrWhiteSpace(pem) && configuration["Licencas:ArquivoChavePrivada"] is { Length: > 0 } arquivo)
        {
            pem = File.ReadAllText(arquivo);
        }
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException(
                "Chave privada não configurada. Defina Licencas__ChavePrivada (conteúdo PEM) ou Licencas__ArquivoChavePrivada.");
        }

        var chave = FormatoLicenca.CarregarChave(pem);
        using var publica = FormatoLicenca.CarregarChave(ChavesLicenca.Publica);
        var teste = "conferencia"u8.ToArray();
        if (!publica.VerifyData(teste, chave.SignData(teste, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256))
        {
            chave.Dispose();
            throw new InvalidOperationException("A chave privada não corresponde à chave pública embutida no PDV (ChavesLicenca).");
        }
        return new Emissor(chave, TimeProvider.System);
    }

    public static Licenca ParaLicenca(LicencaCliente registro) => new(registro.Id, registro.Cliente, registro.Maquina,
        DateOnly.FromDateTime(registro.CriadaEm), registro.ValidaAte);

    public string GerarArquivo(LicencaCliente registro) => FormatoLicenca.Assinar(ParaLicenca(registro), _chave);

    public string Assinar(RespostaVerificacao resposta) => FormatoLicenca.Assinar(resposta, _chave);

    public DateTimeOffset Agora => _relogio.GetUtcNow();

    public void Dispose() => _chave.Dispose();
}
