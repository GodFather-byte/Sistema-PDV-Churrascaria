using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.Tests;

public sealed class DomainRulesTests
{
    [Theory]
    [InlineData("0.001")]
    [InlineData("-1")]
    [InlineData("1000000000000")]
    public void MoneyRejectsInvalidPrecisionOrRange(string amount)
        => Assert.Throws<DomainException>(() => Money.Validate(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void DecimalMoneyRoundTripsExactly()
    {
        Assert.Equal(12345L, Money.ToCents(123.45m));
        Assert.Equal(123.45m, Money.FromCents(12345));
    }

    [Fact]
    public void MixedPaymentChangeComesOnlyFromCash()
    {
        var totals = SaleRules.CalculatePayments(90m, [new(PaymentMethod.Pix, 60), new(PaymentMethod.Cash, 50)]);
        Assert.Equal(20m, totals.Change);
        Assert.Equal(30m, totals.NetCash);
        Assert.Throws<DomainException>(() => SaleRules.CalculatePayments(90, [new(PaymentMethod.Pix, 100)]));
        Assert.Throws<DomainException>(() => SaleRules.CalculatePayments(90, [new(PaymentMethod.Pix, 91), new(PaymentMethod.Cash, 9)]));
        Assert.Throws<DomainException>(() => SaleRules.CalculatePayments(90, [new(PaymentMethod.Cash, 89)]));
        Assert.Throws<DomainException>(() => SaleRules.CalculatePayments(90, [new(PaymentMethod.Cash, 0)]));
    }
}
