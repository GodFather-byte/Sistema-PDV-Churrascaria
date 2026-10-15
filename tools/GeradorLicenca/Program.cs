using System.Globalization;
using System.Security.Cryptography;
using Licenciamento.Comum;

// Ferramenta do fornecedor: cria o par de chaves e emite licenças. Nunca distribua a chave privada.
//   dotnet run --project tools/GeradorLicenca -- chaves  <pasta-fora-do-repositorio>
//   dotnet run --project tools/GeradorLicenca -- emitir  <chave-privada.pem> <codigo-maquina> <cliente> <valida-ate aaaa-mm-dd> [arquivo-saida]

try
{
    switch (args.FirstOrDefault())
    {
        case "chaves" when args.Length == 2:
            CriarChaves(Path.GetFullPath(args[1]));
            break;
        case "emitir" when args.Length is 5 or 6:
            Emitir(args[1], args[2], args[3], args[4], args.Length == 6 ? args[5] : null);
            break;
        case "verificar" when args.Length == 2:
            return Verificar(args[1]);
        case "maquina":
            Console.WriteLine(IdentificacaoMaquina.Obter());
            break;
        default:
            Console.WriteLine("""
                Uso:
                  chaves <pasta>     Cria chave-privada.pem e mostra a chave pública para colar em ChavesLicenca (Licenciamento.Comum).
                  emitir <chave-privada.pem> <codigo-maquina> <cliente> <valida-ate aaaa-mm-dd> [arquivo-saida]
                  verificar <arquivo.pdv>   Mostra os dados da licença se a assinatura conferir com a chave do PDV.
                  maquina            Mostra o código deste computador.
                """);
            return 1;
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or ArgumentException or FormatException or CryptographicException)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return 1;
}

static void CriarChaves(string pasta)
{
    var arquivo = Path.Combine(pasta, "chave-privada.pem");
    if (File.Exists(arquivo)) throw new IOException($"{arquivo} já existe. Nada foi sobrescrito.");
    Directory.CreateDirectory(pasta);
    using var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(arquivo, chave.ExportPkcs8PrivateKeyPem());
    Console.WriteLine($"Chave privada salva em {arquivo}. Faça backup dela: sem ela não é possível emitir licenças.");
    Console.WriteLine("Chave pública:");
    Console.WriteLine(chave.ExportSubjectPublicKeyInfoPem());
}

static int Verificar(string arquivo)
{
    using var chave = ECDsa.Create();
    chave.ImportFromPem(ChavesLicenca.Publica);
    var licenca = FormatoLicenca.Ler<Licenca>(File.ReadAllText(arquivo), chave);
    Console.WriteLine(licenca is null ? "Licença inválida: assinatura não confere com a chave do PDV." : licenca.ToString());
    return licenca is null ? 1 : 0;
}

static void Emitir(string chavePrivada, string maquina, string cliente, string validaAte, string? saida)
{
    using var chave = ECDsa.Create();
    chave.ImportFromPem(File.ReadAllText(chavePrivada));
    var ate = DateOnly.ParseExact(validaAte, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    if (string.IsNullOrWhiteSpace(cliente)) throw new ArgumentException("Informe o cliente.");

    var licenca = new Licenca(Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), cliente.Trim(),
        maquina.Trim().ToUpperInvariant(), DateOnly.FromDateTime(DateTime.Today), ate);
    saida ??= $"licenca-{string.Concat(cliente.Where(char.IsLetterOrDigit))}-{ate:yyyyMMdd}.pdv";
    File.WriteAllText(saida, FormatoLicenca.Assinar(licenca, chave));
    Console.WriteLine($"Licença {licenca.Id} para {licenca.Cliente} (PC {licenca.Maquina}) válida até {ate:dd/MM/yyyy}: {Path.GetFullPath(saida)}");
}
