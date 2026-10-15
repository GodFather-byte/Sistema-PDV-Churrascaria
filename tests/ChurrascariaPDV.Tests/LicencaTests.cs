using System.Security.Cryptography;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Licenciamento;
using Licenciamento.Comum;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public sealed class LicencaTests : IDisposable
{
    private const string Maquina = "AAAA-BBBB-CCCC-DDDD";
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    private sealed class RelogioManual(DateOnly dia) : TimeProvider
    {
        public DateOnly Dia { get; set; } = dia;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(Dia.ToDateTime(new TimeOnly(12, 0)), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)).ToUniversalTime();
    }

    private readonly ECDsa _chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"licenca-{Guid.NewGuid():N}.pdv");
    private readonly RelogioManual _relogio = new(Hoje);

    private LicencaService Criar(AmbienteTeste ambiente) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Licenca:Arquivo"] = _arquivo }).Build(),
        ambiente.Servico<IDbContextFactory<AppDbContext>>(), _relogio, _chave.ExportSubjectPublicKeyInfoPem(), Maquina);

    private string Emitir(DateOnly validaAte, string maquina = Maquina, ECDsa? chave = null) =>
        FormatoLicenca.Assinar(new Licenca("L1", "Cliente Teste", maquina, Hoje, validaAte), chave ?? _chave);

    [Fact]
    public async Task TesteComecaNoPrimeiroTurnoEBloqueiaVendasAoTerminar()
    {
        await using var ambiente = await CriarAsync();
        var licencas = Criar(ambiente);
        Assert.Equal((EstadoLicenca.Teste, LicencaService.DiasTeste), ((await licencas.ObterSituacaoAsync()).Estado, (await licencas.ObterSituacaoAsync()).Dias));

        await ambiente.AbrirCaixaAsync();
        _relogio.Dia = Hoje.AddDays(LicencaService.DiasTeste);
        var situacao = await licencas.ObterSituacaoAsync();
        Assert.Equal(EstadoLicenca.TesteEncerrado, situacao.Estado);
        Assert.False(situacao.PermiteVender);
        await Assert.ThrowsAsync<RegraNegocioException>(licencas.GarantirPodeVenderAsync);
    }

    [Theory]
    [InlineData(30, EstadoLicenca.Ativa, 30)]
    [InlineData(3, EstadoLicenca.VenceEmBreve, 3)]
    [InlineData(-2, EstadoLicenca.Carencia, 3)]
    [InlineData(-6, EstadoLicenca.Vencida, -6)]
    public async Task EstadoDependeDosDiasAteOVencimento(int diasAteVencer, EstadoLicenca esperado, int diasExibidos)
    {
        await using var ambiente = await CriarAsync();
        var licencas = Criar(ambiente);
        File.WriteAllText(_arquivo, Emitir(Hoje.AddDays(diasAteVencer)));

        var situacao = await licencas.ObterSituacaoAsync();
        Assert.Equal(esperado, situacao.Estado);
        Assert.Equal(diasExibidos, situacao.Dias);
        Assert.Equal(esperado != EstadoLicenca.Vencida, situacao.PermiteVender);
    }

    [Fact]
    public async Task RecusaLicencaDeOutroPcAlteradaOuDeOutroEmissor()
    {
        await using var ambiente = await CriarAsync();
        var licencas = Criar(ambiente);
        using var outraChave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var valida = Emitir(Hoje.AddDays(30));
        var adulterada = Convert.ToBase64String("{\"id\":\"L1\",\"cliente\":\"X\",\"maquina\":\"AAAA-BBBB-CCCC-DDDD\",\"emitidaEm\":\"2026-01-01\",\"validaAte\":\"2099-01-01\"}"u8.ToArray())
            + valida[valida.IndexOf('.')..];

        await Assert.ThrowsAsync<RegraNegocioException>(() => licencas.InstalarAsync(Emitir(Hoje.AddDays(30), "ZZZZ-ZZZZ-ZZZZ-ZZZZ")));
        await Assert.ThrowsAsync<RegraNegocioException>(() => licencas.InstalarAsync(adulterada));
        await Assert.ThrowsAsync<RegraNegocioException>(() => licencas.InstalarAsync(Emitir(Hoje.AddDays(30), chave: outraChave)));
        Assert.False(File.Exists(_arquivo));

        Assert.Equal(EstadoLicenca.Ativa, (await licencas.InstalarAsync(valida)).Estado);
    }

    [Fact]
    public async Task AtrasarORelogioNaoProlongaALicenca()
    {
        await using var ambiente = await CriarAsync();
        var licencas = Criar(ambiente);
        File.WriteAllText(_arquivo, Emitir(Hoje.AddDays(-10)));
        await ambiente.AbrirCaixaAsync();

        _relogio.Dia = Hoje.AddDays(-30);
        Assert.Equal(EstadoLicenca.Vencida, (await licencas.ObterSituacaoAsync()).Estado);
    }

    [Fact]
    public async Task LicencaVencidaImpedeAbrirTurnoELancarPedido()
    {
        await using var ambiente = await CriarAsync();
        var licencas = Criar(ambiente);
        File.WriteAllText(_arquivo, Emitir(Hoje.AddDays(-30)));
        var factory = ambiente.Servico<IDbContextFactory<AppDbContext>>();
        var caixa = new CaixaService(factory, ambiente.Servico<ImpressaoService>(), ambiente.Servico<NotificadorMesas>(), licencas);
        var pedidos = new PedidoService(factory, ambiente.Servico<ImpressaoService>(), ambiente.Servico<NotificadorMesas>(), licenca: licencas);

        await Assert.ThrowsAsync<RegraNegocioException>(() => caixa.AbrirTurnoAsync(100m, Gerente));
        await Assert.ThrowsAsync<RegraNegocioException>(() => pedidos.LancarPedidoAsync(
            new NovoPedido(1, null, Garcom, [Item(RodizioAdulto)])));
    }

    [Fact]
    public void CodigoDaMaquinaEhEstavel()
    {
        var codigo = IdentificacaoMaquina.Formatar("guid-exemplo");
        Assert.Equal(codigo, IdentificacaoMaquina.Formatar(" GUID-EXEMPLO "));
        Assert.Matches("^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$", codigo);
    }

    public void Dispose()
    {
        _chave.Dispose();
        File.Delete(_arquivo);
    }
}
