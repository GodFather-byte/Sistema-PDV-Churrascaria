using System.Globalization;
using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Services.Impressao;

public record LinhaTicketProducao(decimal Quantidade, string Produto, string? Observacoes);

public record LinhaTicketConta(decimal Quantidade, string Produto, decimal ValorUnitario, decimal ValorTotal, string? Observacoes);

public record ComandaTicketConta(string Descricao, IReadOnlyList<LinhaTicketConta> Itens, ResumoConta Resumo);

/// <summary>Layouts dos tickets enviados aos setores e ao caixa.</summary>
public static class TicketsImpressao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);

    public static string Quantidade(decimal quantidade) => quantidade.ToString("0.###", PtBr);

    public static string NomeSetor(SetorImpressao setor) => setor switch
    {
        SetorImpressao.BarCopa => "BAR / COPA",
        SetorImpressao.Cozinha => "COZINHA",
        SetorImpressao.Churrasqueira => "CHURRASQUEIRA",
        SetorImpressao.Caixa => "CAIXA",
        _ => "SEM IMPRESSÃO"
    };

    public static DocumentoEscPos Producao(
        DocumentoEscPos doc,
        SetorImpressao setor,
        int numeroMesa,
        string comanda,
        string garcom,
        DateTime dataHora,
        int pedidoId,
        IEnumerable<LinhaTicketProducao> itens,
        bool reimpressao = false)
    {
        doc.Centro(NomeSetor(setor), negrito: true, grande: true);
        if (reimpressao)
        {
            doc.Centro("*** REIMPRESSÃO ***", negrito: true);
        }

        doc.Separador('=')
            .Texto($"MESA {numeroMesa}", negrito: true, grande: true)
            .Texto(comanda, negrito: true)
            .Colunas($"Garçom: {garcom}", dataHora.ToString("dd/MM HH:mm", PtBr))
            .Texto($"Pedido #{pedidoId}")
            .Separador();

        foreach (var item in itens)
        {
            doc.Texto($"{Quantidade(item.Quantidade)} x {item.Produto}", negrito: true, grande: setor != SetorImpressao.BarCopa);
            if (!string.IsNullOrWhiteSpace(item.Observacoes))
            {
                // Na churrasqueira o ponto da carne precisa saltar aos olhos.
                var destaque = setor == SetorImpressao.Churrasqueira;
                doc.Texto($"  >> {(destaque ? item.Observacoes.ToUpper(PtBr) : item.Observacoes)}", negrito: true, grande: destaque);
            }
        }

        return doc.Separador('=');
    }

    public static DocumentoEscPos Cancelamento(
        DocumentoEscPos doc,
        SetorImpressao setor,
        int numeroMesa,
        string comanda,
        string autorizadoPor,
        string motivo,
        LinhaTicketProducao item)
    {
        return doc.Centro(NomeSetor(setor), negrito: true, grande: true)
            .Centro("*** CANCELAMENTO ***", negrito: true, grande: true)
            .Separador('=')
            .Texto($"MESA {numeroMesa}", negrito: true, grande: true)
            .Texto(comanda, negrito: true)
            .Colunas($"Autorizado: {autorizadoPor}", DateTime.Now.ToString("dd/MM HH:mm", PtBr))
            .Separador()
            .Texto($"NÃO PREPARAR: {Quantidade(item.Quantidade)} x {item.Produto}", negrito: true, grande: true)
            .Texto(string.IsNullOrWhiteSpace(item.Observacoes) ? string.Empty : $"  ({item.Observacoes})")
            .Texto($"Motivo: {motivo}")
            .Separador('=');
    }

    public static DocumentoEscPos PreConta(
        DocumentoEscPos doc,
        string estabelecimento,
        int numeroMesa,
        DateTime? abertura,
        int pessoas,
        IReadOnlyList<ComandaTicketConta> comandas)
    {
        doc.Centro(estabelecimento, negrito: true, grande: true)
            .Centro("CONFERÊNCIA DE MESA", negrito: true)
            .Centro("NÃO É DOCUMENTO FISCAL")
            .Separador('=')
            .Colunas($"MESA {numeroMesa}", DateTime.Now.ToString("dd/MM/yyyy HH:mm", PtBr), negrito: true);
        if (abertura is not null)
        {
            doc.Texto($"Entrada: {abertura:HH:mm}  Permanência: {FormatarDuracao(DateTime.Now - abertura.Value)}");
        }

        foreach (var comanda in comandas)
        {
            doc.Separador().Texto(comanda.Descricao.ToUpper(PtBr), negrito: true);
            foreach (var item in comanda.Itens)
            {
                doc.Colunas($"{Quantidade(item.Quantidade)} x {item.Produto}", Moeda(item.ValorTotal));
                if (!string.IsNullOrWhiteSpace(item.Observacoes))
                {
                    doc.Texto($"    {item.Observacoes}");
                }
            }

            doc.Colunas("Subtotal", Moeda(comanda.Resumo.Subtotal));
            doc.Colunas(comanda.Resumo.TaxaServico > 0 ? "Serviço (10%)" : "Serviço (não cobrado)", Moeda(comanda.Resumo.TaxaServico));
            if (comanda.Resumo.Pago > 0)
            {
                doc.Colunas("Já pago", "-" + Moeda(comanda.Resumo.Pago));
            }

            if (comandas.Count > 1)
            {
                doc.Colunas("Saldo da comanda", Moeda(comanda.Resumo.Saldo), negrito: true);
            }
        }

        var geral = comandas.Aggregate(ResumoConta.Vazio, (acc, c) => acc.Somar(c.Resumo));
        doc.Separador('=')
            .Colunas("TOTAL A PAGAR", Moeda(geral.Saldo), negrito: true);
        if (pessoas > 1)
        {
            doc.Texto($"{Moeda(geral.Saldo)} / {pessoas} pessoas = {Moeda(geral.ValorPorPessoa(pessoas))} por pessoa", negrito: true);
        }

        return doc.Separador('=').Centro("O serviço de 10% é opcional.").Centro("Obrigado pela preferência!");
    }

    public static DocumentoEscPos FechamentoTurno(DocumentoEscPos doc, string estabelecimento, ResumoTurno resumo)
    {
        doc.Centro(estabelecimento, negrito: true)
            .Centro("FECHAMENTO DE CAIXA", negrito: true, grande: true)
            .Separador('=')
            .Texto($"Turno #{resumo.TurnoId}  Operador: {resumo.Operador}")
            .Texto($"Abertura:   {resumo.Abertura:dd/MM/yyyy HH:mm}")
            .Texto($"Fechamento: {(resumo.Fechamento ?? DateTime.Now):dd/MM/yyyy HH:mm}")
            .Separador()
            .Texto("RECEBIMENTOS", negrito: true);
        foreach (var meio in resumo.PorMeio)
        {
            doc.Colunas($"{NomeMeio(meio.Meio)} ({meio.Quantidade})", Moeda(meio.Valor));
        }

        doc.Colunas("Total recebido", Moeda(resumo.TotalRecebido), negrito: true)
            .Colunas("  dos quais serviço 10%", Moeda(resumo.TotalTaxaServico))
            .Separador()
            .Texto("GAVETA (DINHEIRO)", negrito: true)
            .Colunas("Fundo de troco", Moeda(resumo.FundoTroco))
            .Colunas("Vendas em dinheiro", Moeda(resumo.TotalDinheiro))
            .Colunas("Suprimentos", Moeda(resumo.TotalSuprimentos))
            .Colunas("Sangrias", "-" + Moeda(resumo.TotalSangrias))
            .Colunas("Esperado na gaveta", Moeda(resumo.DinheiroEsperado), negrito: true);
        if (resumo.DinheiroInformado is not null)
        {
            doc.Colunas("Contado na gaveta", Moeda(resumo.DinheiroInformado.Value))
                .Colunas("Diferença", Moeda(resumo.DinheiroInformado.Value - resumo.DinheiroEsperado), negrito: true);
        }

        return doc.Separador('=')
            .Colunas("Mesas atendidas", resumo.MesasFechadas.ToString(PtBr))
            .Colunas("Itens cancelados", resumo.ItensCancelados.ToString(PtBr))
            .EmBranco()
            .Centro("_______________________________")
            .Centro("Assinatura do operador");
    }

    public static string NomeMeio(MeioPagamento meio) => meio switch
    {
        MeioPagamento.Dinheiro => "Dinheiro",
        MeioPagamento.Pix => "Pix",
        MeioPagamento.CartaoCredito => "Cartão Crédito",
        MeioPagamento.CartaoDebito => "Cartão Débito",
        _ => meio.ToString()
    };

    public static string FormatarDuracao(TimeSpan duracao) =>
        duracao.TotalHours >= 1 ? $"{(int)duracao.TotalHours}h{duracao.Minutes:00}" : $"{Math.Max(0, duracao.Minutes)}min";
}
