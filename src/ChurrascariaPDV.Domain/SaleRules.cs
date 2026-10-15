namespace ChurrascariaPDV.Domain;

public static class Money
{
    public static decimal Validate(decimal value, string field = "Valor", bool positive = false)
    {
        if (value < 0 || (positive && value == 0) || value > 999_999_999_999.99m ||
            decimal.Round(value, 2) != value)
            throw new DomainException($"{field} deve ter até duas casas decimais e ser {(positive ? "positivo" : "não negativo")}.");
        return value;
    }

    public static long ToCents(decimal value) => checked((long)(Validate(value) * 100m));
    public static decimal FromCents(long cents) => cents / 100m;
}

public sealed record PaymentTotals(decimal Total, decimal Change, decimal NetCash);

public static class SaleRules
{
    public static decimal Total(IEnumerable<SaleItem> items, decimal discount)
    {
        Money.Validate(discount, "Desconto");
        var subtotal = Money.Validate(items.Sum(i => i.Subtotal), "Subtotal");
        if (discount > subtotal)
            throw new DomainException("Desconto não pode exceder o subtotal.");
        return subtotal - discount;
    }

    public static PaymentTotals CalculatePayments(decimal total, IReadOnlyList<Payment> payments)
    {
        Money.Validate(total, "Total");
        if (payments.Count == 0)
            throw new DomainException("Informe pelo menos um pagamento.");
        foreach (var payment in payments)
        {
            if (!Enum.IsDefined(payment.Method))
                throw new DomainException("Forma de pagamento inválida.");
            Money.Validate(payment.Amount, "Pagamento", positive: true);
        }
        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var nonCash = payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);
        if (nonCash > total || cash + nonCash < total)
            throw new DomainException("Pagamentos insuficientes ou troco excede o pagamento em dinheiro.");
        var change = Money.Validate(cash + nonCash - total, "Troco");
        return new(total, change, cash - change);
    }
}
