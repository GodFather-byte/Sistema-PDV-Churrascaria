using System.Security.Cryptography;
using LicencasServidor.Data;
using LicencasServidor.Services;
using Licenciamento.Comum;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LicencasServidor.Tests;

public sealed class VerificacaoTests : IAsyncDisposable
{
    private const string Maquina = "AAAA-BBBB-CCCC-DDDD";
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly ECDsa _chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly LicencasService _servico;
    private readonly AdministradoresService _admins;
    private readonly FabricaContexto _fabrica;

    private sealed class FabricaContexto(SqliteConnection conexao) : IDbContextFactory<LicencasDbContext>
    {
        public LicencasDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<LicencasDbContext>().UseSqlite(conexao).Options);
    }

    public VerificacaoTests()
    {
        _conexao.Open();
        _fabrica = new FabricaContexto(_conexao);
        using (var db = _fabrica.CreateDbContext()) db.Database.Migrate();
        _servico = new LicencasService(_fabrica, new Emissor(_chave, TimeProvider.System));
        _admins = new AdministradoresService(_fabrica, new LimiteTentativas(TimeProvider.System));
    }

    private RespostaVerificacao Ler(string texto) => FormatoLicenca.Ler<RespostaVerificacao>(texto, _chave)!;

    private Task<LicencaCliente> CadastrarAsync(bool bloqueada = false) => _servico.SalvarAsync(new LicencaCliente
    {
        Cliente = "Pizzaria Teste", Maquina = "aaaa-bbbb-cccc-dddd", ValidaAte = new DateOnly(2027, 1, 31),
        Bloqueada = bloqueada, MotivoBloqueio = bloqueada ? "Mensalidade em atraso" : null
    });

    [Fact]
    public async Task CaixaSemLicencaRecebeALicencaCadastradaParaSuaMaquina()
    {
        var registro = await CadastrarAsync();
        var resposta = Ler(await _servico.VerificarAsync(new PedidoVerificacao(null, Maquina, "1.1.0"), "200.1.2.3"));

        Assert.Equal(StatusVerificacao.Ativa, resposta.Status);
        var licenca = FormatoLicenca.Ler<Licenca>(resposta.ArquivoLicenca!, _chave)!;
        Assert.Equal((registro.Id, Maquina, new DateOnly(2027, 1, 31)), (licenca.Id, licenca.Maquina, licenca.ValidaAte));

        var salvo = (await _servico.ObterAsync(registro.Id))!;
        Assert.Equal(("1.1.0", "200.1.2.3", 1), (salvo.UltimaVersao, salvo.UltimoIp, salvo.TotalVerificacoes));
        Assert.NotNull(salvo.UltimaVerificacao);
    }

    [Fact]
    public async Task LicencaBloqueadaRespondeBloqueioComMotivo()
    {
        var registro = await CadastrarAsync(bloqueada: true);
        var resposta = Ler(await _servico.VerificarAsync(new PedidoVerificacao(registro.Id, Maquina, null), null));

        Assert.Equal((StatusVerificacao.Bloqueada, registro.Id, "Mensalidade em atraso"), (resposta.Status, resposta.LicencaId, resposta.Mensagem));
        Assert.Null(resposta.ArquivoLicenca);
    }

    [Fact]
    public async Task LicencaDeOutraMaquinaNaoEhEntregue()
    {
        var registro = await CadastrarAsync();
        var resposta = Ler(await _servico.VerificarAsync(new PedidoVerificacao(registro.Id, "ZZZZ-ZZZZ-ZZZZ-ZZZZ", null), null));

        Assert.Equal(StatusVerificacao.NaoEncontrada, resposta.Status);
        Assert.Null(resposta.ArquivoLicenca);
        await Assert.ThrowsAsync<RegraException>(() => _servico.VerificarAsync(new PedidoVerificacao(null, "invalido", null), null));
    }

    [Fact]
    public async Task CadastroExigeMaquinaValidaEMotivoDoBloqueio()
    {
        await Assert.ThrowsAsync<RegraException>(() => _servico.SalvarAsync(new LicencaCliente { Cliente = "X", Maquina = "123" }));
        await Assert.ThrowsAsync<RegraException>(() => _servico.SalvarAsync(new LicencaCliente { Cliente = "X", Maquina = Maquina, Bloqueada = true }));
    }

    [Fact]
    public async Task PainelExigeSenhaForteEBloqueiaTentativasRepetidas()
    {
        await Assert.ThrowsAsync<RegraException>(() => _admins.CriarAsync("dono", "curta"));
        await _admins.CriarAsync("Dono", "senha-bem-forte-123");
        Assert.NotNull(await _admins.AutenticarAsync("dono", "senha-bem-forte-123", "ip1"));

        for (var i = 0; i < 5; i++) Assert.Null(await _admins.AutenticarAsync("dono", "errada", "ip2"));
        await Assert.ThrowsAsync<RegraException>(() => _admins.AutenticarAsync("dono", "senha-bem-forte-123", "ip2"));
        Assert.NotNull(await _admins.AutenticarAsync("dono", "senha-bem-forte-123", "ip1"));
    }

    public async ValueTask DisposeAsync()
    {
        _chave.Dispose();
        await _conexao.DisposeAsync();
    }
}
