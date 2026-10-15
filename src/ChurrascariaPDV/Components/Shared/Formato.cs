using System.Globalization;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services.Impressao;

namespace ChurrascariaPDV.Components.Shared;

public static class Formato
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);

    public static string Numero(decimal valor) => valor.ToString("N2", PtBr);

    public static string Qtd(decimal quantidade) => quantidade.ToString("0.###", PtBr);

    public static string Hora(DateTime data) => data.ToString("HH:mm", PtBr);

    public static string DataHora(DateTime data) => data.ToString("dd/MM HH:mm", PtBr);

    public static string Duracao(DateTime? desde) =>
        desde is null ? string.Empty : TicketsImpressao.FormatarDuracao(DateTime.Now - desde.Value);

    public static string Meio(MeioPagamento meio) => TicketsImpressao.NomeMeio(meio);

    public static string Status(StatusMesa status) => status switch
    {
        StatusMesa.Livre => "Livre",
        StatusMesa.Ocupada => "Ocupada",
        StatusMesa.ContaSolicitada => "Conta pedida",
        StatusMesa.AguardandoLimpeza => "Limpeza",
        _ => status.ToString()
    };

    public static string ClasseStatus(StatusMesa status) => status switch
    {
        StatusMesa.Livre => "livre",
        StatusMesa.Ocupada => "ocupada",
        StatusMesa.ContaSolicitada => "conta",
        StatusMesa.AguardandoLimpeza => "limpeza",
        _ => string.Empty
    };

    public static string Perfil(PerfilUsuario perfil) => perfil switch
    {
        PerfilUsuario.Garcom => "Garçom",
        PerfilUsuario.OperadorCaixa => "Operador de Caixa",
        PerfilUsuario.Gerente => "Gerente",
        _ => perfil.ToString()
    };

    public static string TipoEstabelecimento(TipoEstabelecimento tipo) => tipo switch
    {
        Domain.Enums.TipoEstabelecimento.Bar => "Bar",
        Domain.Enums.TipoEstabelecimento.Restaurante => "Restaurante",
        Domain.Enums.TipoEstabelecimento.Churrascaria => "Churrascaria",
        Domain.Enums.TipoEstabelecimento.Pizzaria => "Pizzaria",
        Domain.Enums.TipoEstabelecimento.Hamburgueria => "Hamburgueria",
        Domain.Enums.TipoEstabelecimento.Outro => "Outro",
        _ => tipo.ToString()
    };

    public static string Setor(SetorImpressao setor) => setor switch
    {
        SetorImpressao.Nenhum => "Sem impressão",
        SetorImpressao.BarCopa => "Bar / Copa",
        SetorImpressao.Cozinha => "Cozinha",
        SetorImpressao.Churrasqueira => "Churrasqueira",
        SetorImpressao.Caixa => "Caixa",
        _ => setor.ToString()
    };

    public static string TipoItem(TipoItem tipo) => tipo switch
    {
        Domain.Enums.TipoItem.Rodizio => "Rodízio",
        Domain.Enums.TipoItem.CarneAlaCarte => "Carne à la carte",
        Domain.Enums.TipoItem.PorcaoCozinha => "Porção (cozinha)",
        Domain.Enums.TipoItem.Bebida => "Bebida",
        Domain.Enums.TipoItem.Sobremesa => "Sobremesa",
        _ => tipo.ToString()
    };

    /// <summary>Aceita "110,50", "110.50", "1.234,56" e "R$ 10".</summary>
    public static bool TentarLerValor(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpo = texto.Replace("R$", string.Empty).Replace(" ", string.Empty).Trim();
        if (limpo.Contains(','))
        {
            limpo = limpo.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (limpo.Count(c => c == '.') > 1)
        {
            limpo = limpo.Replace(".", string.Empty);
        }

        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    public static string ValorEditavel(decimal valor) => valor.ToString("0.00", PtBr);
}
