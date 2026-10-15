using ChurrascariaPDV.Services.Impressao;

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
}
