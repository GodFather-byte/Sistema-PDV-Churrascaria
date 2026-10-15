using System.Globalization;
using System.Text;

namespace ChurrascariaPDV.Services.Impressao;

public enum AlinhamentoTexto
{
    Esquerda,
    Centro,
    Direita
}

/// <summary>
/// Monta um ticket ESC/POS. O mesmo documento gera os bytes da impressora térmica e
/// uma versão em texto puro usada no modo simulação.
/// </summary>
public class DocumentoEscPos(int largura)
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;
    private const int CodePagePortugues = 860;

    private readonly List<(string Texto, bool Negrito, bool Grande, AlinhamentoTexto Alinhamento)> _linhas = [];

    public int Largura { get; } = largura;

    public DocumentoEscPos Texto(string texto, bool negrito = false, bool grande = false, AlinhamentoTexto alinhamento = AlinhamentoTexto.Esquerda)
    {
        var colunas = grande ? Largura / 2 : Largura;
        foreach (var linha in Quebrar(texto, colunas))
        {
            _linhas.Add((linha, negrito, grande, alinhamento));
        }

        return this;
    }

    public DocumentoEscPos Centro(string texto, bool negrito = false, bool grande = false) =>
        Texto(texto, negrito, grande, AlinhamentoTexto.Centro);

    public DocumentoEscPos Separador(char caractere = '-') => Texto(new string(caractere, Largura));

    public DocumentoEscPos EmBranco() => Texto(string.Empty);

    /// <summary>Texto à esquerda e valor alinhado à direita na mesma linha.</summary>
    public DocumentoEscPos Colunas(string esquerda, string direita, bool negrito = false)
    {
        var espacoEsquerda = Math.Max(1, Largura - direita.Length - 1);
        if (esquerda.Length > espacoEsquerda)
        {
            esquerda = esquerda[..espacoEsquerda];
        }

        return Texto(esquerda.PadRight(Largura - direita.Length) + direita, negrito);
    }

    public string ParaTexto()
    {
        var sb = new StringBuilder();
        foreach (var (texto, _, _, alinhamento) in _linhas)
        {
            sb.AppendLine(Alinhar(texto, Largura, alinhamento));
        }

        return sb.ToString();
    }

    public byte[] ParaBytes()
    {
        var encoding = ObterEncoding();
        var bytes = new List<byte>();
        bytes.AddRange([Esc, (byte)'@']);          // inicializa
        bytes.AddRange([Esc, (byte)'t', 3]);        // tabela de caracteres PC860 (português)

        foreach (var (texto, negrito, grande, alinhamento) in _linhas)
        {
            bytes.AddRange([Esc, (byte)'a', (byte)alinhamento]);
            bytes.AddRange([Esc, (byte)'E', (byte)(negrito ? 1 : 0)]);
            bytes.AddRange([Gs, (byte)'!', (byte)(grande ? 0x11 : 0x00)]);
            bytes.AddRange(encoding.GetBytes(texto));
            bytes.Add((byte)'\n');
        }

        bytes.AddRange([Esc, (byte)'E', 0, Gs, (byte)'!', 0, Esc, (byte)'a', 0]);
        bytes.AddRange([Esc, (byte)'d', 4]);        // avança papel
        bytes.AddRange([Gs, (byte)'V', 66, 0]);     // corte parcial
        return bytes.ToArray();
    }

    private static Encoding ObterEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CodePagePortugues);
        }
        catch (Exception)
        {
            return new AsciiSemAcentos();
        }
    }

    private static string Alinhar(string texto, int largura, AlinhamentoTexto alinhamento)
    {
        if (texto.Length >= largura || alinhamento == AlinhamentoTexto.Esquerda)
        {
            return texto;
        }

        var sobra = largura - texto.Length;
        return alinhamento == AlinhamentoTexto.Centro
            ? new string(' ', sobra / 2) + texto
            : new string(' ', sobra) + texto;
    }

    private static IEnumerable<string> Quebrar(string texto, int colunas)
    {
        if (string.IsNullOrEmpty(texto))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (var paragrafo in texto.Split('\n'))
        {
            var restante = paragrafo.TrimEnd();
            if (restante.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            while (restante.Length > colunas)
            {
                var corte = restante.LastIndexOf(' ', colunas);
                if (corte <= 0)
                {
                    corte = colunas;
                }

                yield return restante[..corte].TrimEnd();
                restante = restante[corte..].TrimStart();
            }

            yield return restante;
        }
    }

    /// <summary>Fallback que remove acentos caso a code page 860 não esteja disponível.</summary>
    private sealed class AsciiSemAcentos : ASCIIEncoding
    {
        public override byte[] GetBytes(string s)
        {
            var normalizado = s.Normalize(NormalizationForm.FormD);
            var semAcentos = new string(normalizado
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
            return base.GetBytes(semAcentos);
        }
    }
}
