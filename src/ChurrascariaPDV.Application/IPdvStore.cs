using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.Application;

// The store owns transaction boundaries; mutations must be serialized before reading state.
public interface IPdvStore
{
    T Read<T>(Func<PdvState, T> action);
    T Write<T>(Func<PdvState, T> action);
}

public sealed class PdvState
{
    public List<Product> Products { get; } = [];
    public List<StoredSession> Sessions { get; } = [];
    public List<StoredSale> Sales { get; } = [];
    public List<StockMovement> StockMovements { get; } = [];
    public List<StoredCashMovement> CashMovements { get; } = [];
    public List<AuditEntry> Audit { get; } = [];
}

public sealed record StoredSession(CashSession Session, decimal? CountedCash);
public sealed record StoredSale(Sale Sale, IReadOnlyList<Payment> Payments);
public sealed record StoredCashMovement(long Id, long SessionId, CashMovementType Type,
    decimal Amount, string Reason, string Operator, DateTime CreatedAt);
public sealed record AuditEntry(long Id, string Entity, long EntityId, string Action,
    string Operator, DateTime CreatedAt, string Details);
