namespace ChurrascariaPDV.Domain;

public enum PaymentMethod { Cash, CreditCard, DebitCard, Pix }
public enum StockMovementType { Entry, Exit, Adjustment }
public enum CashMovementType { Withdrawal, Supply }
public enum SaleStatus { Open, Completed, Cancelled }

public sealed record Product(long Id, string Sku, string Name, string Category,
    decimal Price, decimal? Cost, bool Active, int Stock);
public sealed record ProductInput(string Sku, string Name, string Category,
    decimal Price, decimal? Cost, bool Active);
public sealed record SaleItem(long ProductId, string Sku, string Name, int Quantity, decimal UnitPrice)
{
    public decimal Subtotal => Quantity * UnitPrice;
}
public sealed record Payment(PaymentMethod Method, decimal Amount);
public sealed record Sale(long Id, long CashSessionId, string Operator, DateTime CreatedAt,
    SaleStatus Status, IReadOnlyList<SaleItem> Items, decimal Discount, decimal Change)
{
    public decimal Subtotal => Items.Sum(item => item.Subtotal);
    public decimal Total => Subtotal - Discount;
}
public sealed record StockMovement(long Id, long ProductId, string ProductName,
    StockMovementType Type, int Quantity, string Reason, string Operator, DateTime CreatedAt);
public sealed record CashSession(long Id, string Operator, DateTime OpenedAt,
    decimal OpeningAmount, DateTime? ClosedAt);
public sealed record CashSummary(long SessionId, decimal OpeningAmount,
    IReadOnlyDictionary<PaymentMethod, decimal> SalesByMethod, decimal Supplies,
    decimal Withdrawals, decimal ExpectedCash, decimal? CountedCash, decimal? Difference);
public sealed record SaleReport(long Id, DateTime CreatedAt, string Operator,
    SaleStatus Status, decimal Subtotal, decimal Discount, decimal Total);
public sealed record BestSeller(long ProductId, string Name, int Quantity, decimal GrossRevenue);

public sealed class DomainException(string message) : Exception(message);
