using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChurrascariaPDV.Tests;

public class DocumentoEscPosTests
{
    [Fact]
    public void GeraComandosEscPosComCorte()
    {
        var bytes = new DocumentoEscPos(32).Centro("MESA 12", negrito: true, grande: true).Texto("Ação").ParaBytes();

        Assert.Equal([0x1B, (byte)'@'], bytes[..2]);
        Assert.Equal([0x1D, (byte)'V', 66, 0], bytes[^4..]);
    }

    [Fact]
    public void QuebraLinhasLongas()
    {
        var texto = new DocumentoEscPos(10).Texto("Picanha na tábua bem passada").ParaTexto();

        Assert.All(texto.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), linha => Assert.True(linha.Length <= 10));
    }

    [Theory]
    [InlineData("Bar / Copa", false, false)]
    [InlineData("Cozinha", true, false)]
    [InlineData("Churrasqueira", true, true)]
    public void ProducaoComSetorCadastradoPreservaBytesAnteriores(string nome, bool itensGrandes, bool destacar)
    {
        var hora = new DateTime(2026, 10, 15, 12, 0, 0);
        var setor = new SetorImpressao { Nome = nome, ItensEmDestaque = itensGrandes, ObservacoesEmDestaque = destacar };
        var atual = TicketsImpressao.Producao(new DocumentoEscPos(48), setor, 1, "Conta Geral", "João", hora, 1,
            [new LinhaTicketProducao(1, "Produto", "Ao Ponto")]).ParaBytes();
        var anterior = new DocumentoEscPos(48)
            .Centro(nome.ToUpperInvariant(), negrito: true, grande: true)
            .Separador('=')
            .Texto("MESA 1", negrito: true, grande: true)
            .Texto("Conta Geral", negrito: true)
            .Colunas("Garçom: João", "15/10 12:00")
            .Texto("Pedido #1")
            .Separador()
            .Texto("1 x Produto", negrito: true, grande: itensGrandes)
            .Texto(destacar ? "  >> AO PONTO" : "  >> Ao Ponto", negrito: true, grande: destacar)
            .Separador('=').ParaBytes();

        Assert.Equal(anterior, atual);
    }

    [Fact]
    public void LarguraPadraoDoPerfilMantemBytesEscPosExistentes()
    {
        var perfil = new PerfilEstabelecimento();
        var options = new OptionsMonitorFalso<ImpressaoOptions>(new ImpressaoOptions());
        var servico = new ImpressaoService(options, new HistoricoImpressoes(), NullLogger<ImpressaoService>.Instance);
        var esperado = new DocumentoEscPos(48).Centro("CHURRASCARIA", negrito: true).Texto("Ação").ParaBytes();
        var atual = servico.NovoDocumento(perfil).Centro("CHURRASCARIA", negrito: true).Texto("Ação").ParaBytes();

        Assert.Equal(esperado, atual);
    }

    [Fact]
    public void PreContaComPerfilPadraoMantemSaidaDoTicketAnterior()
    {
        var agora = new DateTime(2026, 10, 15, 12, 0, 0);
        var comanda = new ComandaTicketConta(
            "Conta Geral",
            [new LinhaTicketConta(1, "Chopp", 14.90m, 14.90m, null)],
            new ResumoConta(14.90m, 1.49m, 16.39m, 0, 16.39m));
        var atual = TicketsImpressao.PreConta(
            new DocumentoEscPos(48), "CHURRASCARIA", 1, null, 1, [comanda], 10m, true, agora).ParaBytes();

        var anterior = new DocumentoEscPos(48)
            .Centro("CHURRASCARIA", negrito: true, grande: true)
            .Centro("CONFERÊNCIA DE MESA", negrito: true)
            .Centro("NÃO É DOCUMENTO FISCAL")
            .Separador('=')
            .Colunas("MESA 1", agora.ToString("dd/MM/yyyy HH:mm"), negrito: true)
            .Separador()
            .Texto("CONTA GERAL", negrito: true)
            .Colunas("1 x Chopp", TicketsImpressao.Moeda(14.90m))
            .Colunas("Subtotal", TicketsImpressao.Moeda(14.90m))
            .Colunas("Serviço (10%)", TicketsImpressao.Moeda(1.49m))
            .Separador('=')
            .Colunas("TOTAL A PAGAR", TicketsImpressao.Moeda(16.39m), negrito: true)
            .Separador('=')
            .Centro("O serviço de 10% é opcional.")
            .Centro("Obrigado pela preferência!")
            .ParaBytes();

        Assert.Equal(anterior, atual);
    }

    [Fact]
    public void FechamentoComPerfilPadraoMantemLinhaDeServicoAnterior()
    {
        var hora = new DateTime(2026, 10, 15, 12, 0, 0);
        var resumo = new ResumoTurno(
            1, "Gerente", hora, hora, 100m, [], 0m, 0m, 0m, 0m, 0m, 100m, 100m, 0, 0, []);
        var atual = TicketsImpressao.FechamentoTurno(new DocumentoEscPos(48), "CHURRASCARIA", resumo, 10m).ParaBytes();
        var anterior = new DocumentoEscPos(48)
            .Centro("CHURRASCARIA", negrito: true)
            .Centro("FECHAMENTO DE CAIXA", negrito: true, grande: true)
            .Separador('=')
            .Texto("Turno #1  Operador: Gerente")
            .Texto($"Abertura:   {hora:dd/MM/yyyy HH:mm}")
            .Texto($"Fechamento: {hora:dd/MM/yyyy HH:mm}")
            .Separador()
            .Texto("RECEBIMENTOS", negrito: true)
            .Colunas("Total recebido", TicketsImpressao.Moeda(0), negrito: true)
            .Colunas("  dos quais serviço 10%", TicketsImpressao.Moeda(0))
            .Separador()
            .Texto("GAVETA (DINHEIRO)", negrito: true)
            .Colunas("Fundo de troco", TicketsImpressao.Moeda(100))
            .Colunas("Vendas em dinheiro", TicketsImpressao.Moeda(0))
            .Colunas("Suprimentos", TicketsImpressao.Moeda(0))
            .Colunas("Sangrias", "-" + TicketsImpressao.Moeda(0))
            .Colunas("Esperado na gaveta", TicketsImpressao.Moeda(100), negrito: true)
            .Colunas("Contado na gaveta", TicketsImpressao.Moeda(100))
            .Colunas("Diferença", TicketsImpressao.Moeda(0), negrito: true)
            .Separador('=')
            .Colunas("Mesas atendidas", "0")
            .Colunas("Itens cancelados", "0")
            .EmBranco()
            .Centro("_______________________________")
            .Centro("Assinatura do operador")
            .ParaBytes();

        Assert.Equal(anterior, atual);
    }

    private sealed class OptionsMonitorFalso<T>(T valor) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue => valor;

        public T Get(string? name) => valor;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
