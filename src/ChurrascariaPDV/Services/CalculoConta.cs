using ChurrascariaPDV.Domain.Entities;

namespace ChurrascariaPDV.Services;

public record ResumoConta(decimal Subtotal, decimal TaxaServico, decimal Total, decimal Pago, decimal Saldo)
{
    public static readonly ResumoConta Vazio = new(0, 0, 0, 0, 0);

    public ResumoConta Somar(ResumoConta outro) => new(
        Subtotal + outro.Subtotal,
        TaxaServico + outro.TaxaServico,
        Total + outro.Total,
        Pago + outro.Pago,
        Saldo + outro.Saldo);

    public decimal ValorPorPessoa(int pessoas) => CalculoConta.Arredondar(pessoas <= 1 ? Saldo : Saldo / pessoas);
}

public static class CalculoConta
{
    public const decimal PercentualTaxaServico = 0.10m;

    public static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static decimal CalcularTaxa(decimal subtotal, bool cobrarTaxa) =>
        cobrarTaxa ? Arredondar(subtotal * PercentualTaxaServico) : 0m;

    /// <summary>Calcula a conta de uma comanda. Exige Pedidos.Itens e Pagamentos carregados.</summary>
    public static ResumoConta Calcular(Comanda comanda)
    {
        var subtotal = comanda.Pedidos
            .SelectMany(pedido => pedido.Itens)
            .Where(item => !item.Cancelado)
            .Sum(item => item.ValorTotal);
        var taxa = CalcularTaxa(subtotal, comanda.CobrarTaxaServico);
        var total = subtotal + taxa;
        var pago = comanda.Pagamentos.Where(pagamento => !pagamento.Estornado).Sum(pagamento => pagamento.ValorPago);
        return new ResumoConta(subtotal, taxa, total, pago, total - pago);
    }

    public static ResumoConta Calcular(IEnumerable<Comanda> comandas) =>
        comandas.Aggregate(ResumoConta.Vazio, (acumulado, comanda) => acumulado.Somar(Calcular(comanda)));

    /// <summary>Parte da taxa de serviço contida em um pagamento, proporcional ao total da conta.</summary>
    public static decimal TaxaProporcional(decimal valorPago, ResumoConta conta) =>
        conta.Total <= 0 ? 0 : Arredondar(valorPago * conta.TaxaServico / conta.Total);
}
