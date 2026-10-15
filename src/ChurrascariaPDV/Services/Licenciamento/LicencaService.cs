using System.Security.Cryptography;
using ChurrascariaPDV.Infrastructure.Data;
using Licenciamento.Comum;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services.Licenciamento;

public enum EstadoLicenca { Ativa, VenceEmBreve, Carencia, Vencida, Bloqueada, Teste, TesteEncerrado }

public record SituacaoLicenca(EstadoLicenca Estado, Licenca? Licenca, int Dias, string CodigoMaquina, string? Problema)
{
    /// <summary>Sem licença válida o sistema fica somente leitura: não abre turno nem lança pedidos.</summary>
    public bool PermiteVender => Estado is not (EstadoLicenca.Vencida or EstadoLicenca.Bloqueada or EstadoLicenca.TesteEncerrado);

    public bool ExibirAviso => Estado is not EstadoLicenca.Ativa;
}

/// <summary>
/// Licenciamento por assinatura com arquivo offline preso ao PC do caixa, renovado ou bloqueado pelo
/// servidor de licenças quando há internet. Sem licença, roda em teste por <see cref="DiasTeste"/> dias
/// a partir da abertura do primeiro turno.
/// </summary>
public class LicencaService(IConfiguration configuration, IDbContextFactory<AppDbContext> dbFactory,
    TimeProvider relogio, string? chavePublicaPem = null, string? codigoMaquina = null)
{
    public const int DiasTeste = 15;
    public const int DiasAviso = 7;
    public const int DiasCarencia = 5;

    public LicencaService(IConfiguration configuration, IDbContextFactory<AppDbContext> dbFactory)
        : this(configuration, dbFactory, TimeProvider.System) { }

    public string CodigoMaquina { get; } = codigoMaquina ?? IdentificacaoMaquina.Obter();

    private string Arquivo => Path.GetFullPath(configuration["Licenca:Arquivo"] ?? "licenca.pdv");

    /// <summary>Resposta assinada do servidor informando o bloqueio da licença atual.</summary>
    private string ArquivoBloqueio => Path.ChangeExtension(Arquivo, ".bloqueio");

    public async Task<SituacaoLicenca> ObterSituacaoAsync()
    {
        var (licenca, problema) = LerArquivo();
        var hoje = await DataEfetivaAsync();

        if (licenca is not null)
        {
            if (LerBloqueio(licenca) is { } bloqueio)
            {
                return new SituacaoLicenca(EstadoLicenca.Bloqueada, licenca, 0, CodigoMaquina, bloqueio.Mensagem);
            }

            var dias = licenca.ValidaAte.DayNumber - hoje.DayNumber;
            var estado = dias switch
            {
                > DiasAviso => EstadoLicenca.Ativa,
                >= 0 => EstadoLicenca.VenceEmBreve,
                >= -DiasCarencia => EstadoLicenca.Carencia,
                _ => EstadoLicenca.Vencida
            };
            // Na carência, Dias indica quantos dias faltam para o bloqueio de vendas.
            return new SituacaoLicenca(estado, licenca, estado == EstadoLicenca.Carencia ? DiasCarencia + dias : dias,
                CodigoMaquina, null);
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var primeiroTurno = await db.TurnosCaixa.MinAsync(t => (DateTime?)t.DataHoraAbertura);
        var inicioTeste = primeiroTurno is { } inicio ? DateOnly.FromDateTime(inicio) : hoje;
        var restantes = inicioTeste.DayNumber + DiasTeste - hoje.DayNumber;
        return new SituacaoLicenca(restantes > 0 ? EstadoLicenca.Teste : EstadoLicenca.TesteEncerrado, null,
            Math.Max(0, restantes), CodigoMaquina, problema);
    }

    public async Task GarantirPodeVenderAsync()
    {
        var situacao = await ObterSituacaoAsync();
        if (situacao.PermiteVender) return;
        throw new RegraNegocioException(
            (situacao.Estado == EstadoLicenca.Bloqueada ? "Licença bloqueada pelo fornecedor" : "Licença vencida") +
            ": o sistema está somente leitura. Ainda é possível receber contas abertas, fechar o caixa " +
            "e ver relatórios. Fale com o fornecedor ou veja Gerência > Licença.");
    }

    /// <summary>Valida e grava um novo arquivo de licença, substituindo o anterior.</summary>
    public async Task<SituacaoLicenca> InstalarAsync(string conteudo)
    {
        var licenca = Validar(conteudo, out var problema) ?? throw new RegraNegocioException(problema!);
        await File.WriteAllTextAsync(Arquivo, conteudo.Trim());
        var situacao = await ObterSituacaoAsync();
        if (situacao.Licenca?.Id != licenca.Id)
        {
            throw new RegraNegocioException("A licença foi gravada, mas não pôde ser lida de volta. Verifique a pasta do PDV.");
        }
        return situacao;
    }

    /// <summary>Dados para a conferência online: a licença atual (se houver) e o código deste PC.</summary>
    public PedidoVerificacao CriarPedidoVerificacao(string? versao) => new(LerArquivo().Licenca?.Id, CodigoMaquina, versao);

    /// <summary>
    /// Aplica a resposta assinada do servidor: grava a licença renovada ou registra o bloqueio.
    /// Respostas sem assinatura válida, de outra máquina ou "não encontrada" não alteram nada.
    /// </summary>
    public async Task<RespostaVerificacao> AplicarRespostaOnlineAsync(string respostaAssinada)
    {
        using var chave = CarregarChavePublica();
        var resposta = FormatoLicenca.Ler<RespostaVerificacao>(respostaAssinada, chave)
            ?? throw new RegraNegocioException("Resposta do servidor de licenças sem assinatura válida.");
        if (!string.Equals(resposta.Maquina, CodigoMaquina, StringComparison.OrdinalIgnoreCase))
            throw new RegraNegocioException("Resposta do servidor de licenças destinada a outro computador.");

        switch (resposta.Status)
        {
            case StatusVerificacao.Ativa when resposta.ArquivoLicenca is { } arquivo:
                var atual = File.Exists(Arquivo) ? (await File.ReadAllTextAsync(Arquivo)).Trim() : null;
                if (atual != arquivo.Trim()) await InstalarAsync(arquivo);
                File.Delete(ArquivoBloqueio);
                break;
            case StatusVerificacao.Bloqueada when resposta.LicencaId == LerArquivo().Licenca?.Id:
                await File.WriteAllTextAsync(ArquivoBloqueio, respostaAssinada.Trim());
                break;
        }
        return resposta;
    }

    private (Licenca? Licenca, string? Problema) LerArquivo()
    {
        if (!File.Exists(Arquivo)) return (null, null);
        var licenca = Validar(File.ReadAllText(Arquivo), out var problema);
        return (licenca, problema);
    }

    private RespostaVerificacao? LerBloqueio(Licenca licenca)
    {
        if (!File.Exists(ArquivoBloqueio)) return null;
        using var chave = CarregarChavePublica();
        var resposta = FormatoLicenca.Ler<RespostaVerificacao>(File.ReadAllText(ArquivoBloqueio), chave);
        return resposta is { Status: StatusVerificacao.Bloqueada } && resposta.LicencaId == licenca.Id
            && string.Equals(resposta.Maquina, CodigoMaquina, StringComparison.OrdinalIgnoreCase)
            ? resposta
            : null;
    }

    private Licenca? Validar(string conteudo, out string? problema)
    {
        using var chave = CarregarChavePublica();
        var licenca = FormatoLicenca.Ler<Licenca>(conteudo, chave);
        problema = licenca switch
        {
            null => "Arquivo de licença inválido ou alterado.",
            _ when !string.Equals(licenca.Maquina, CodigoMaquina, StringComparison.OrdinalIgnoreCase) =>
                $"Esta licença é de outro computador ({licenca.Maquina}). Código deste PC: {CodigoMaquina}.",
            _ => null
        };
        return problema is null ? licenca : null;
    }

    private ECDsa CarregarChavePublica() => FormatoLicenca.CarregarChave(chavePublicaPem ?? ChavesLicenca.Publica);

    /// <summary>
    /// Hoje, ou a data do registro mais recente se o relógio do PC tiver sido atrasado,
    /// para que voltar a data do Windows não prolongue a licença.
    /// </summary>
    private async Task<DateOnly> DataEfetivaAsync()
    {
        var agora = relogio.GetLocalNow().DateTime;
        await using var db = await dbFactory.CreateDbContextAsync();
        var ultimoPedido = await db.Pedidos.MaxAsync(p => (DateTime?)p.DataHoraLancamento) ?? DateTime.MinValue;
        var ultimoPagamento = await db.Pagamentos.MaxAsync(p => (DateTime?)p.DataHora) ?? DateTime.MinValue;
        var ultimoTurno = await db.TurnosCaixa.MaxAsync(t => (DateTime?)t.DataHoraAbertura) ?? DateTime.MinValue;
        var maior = new[] { agora, ultimoPedido, ultimoPagamento, ultimoTurno }.Max();
        return DateOnly.FromDateTime(maior);
    }
}
