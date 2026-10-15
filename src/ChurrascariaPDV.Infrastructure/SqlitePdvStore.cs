using System.Globalization;
using ChurrascariaPDV.Application;
using ChurrascariaPDV.Domain;
using Microsoft.Data.Sqlite;

namespace ChurrascariaPDV.Infrastructure;

public sealed class SqlitePdvStore : IPdvStore
{
    private readonly string connectionString;

    public SqlitePdvStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || databasePath == ":memory:")
            throw new ArgumentException("Informe o caminho de um banco SQLite persistente.", nameof(databasePath));
        var path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path, ForeignKeys = true, DefaultTimeout = 30, Pooling = false
        }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var version = Convert.ToInt32(Scalar(connection, transaction, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
        if (version > 1) throw new DomainException("Versão do banco mais recente que esta aplicação.");
        if (version == 0)
        {
            using var stream = typeof(SqlitePdvStore).Assembly.GetManifestResourceStream(
                "ChurrascariaPDV.Infrastructure.Migrations.001_Initial.sql")
                ?? throw new InvalidOperationException("Migração inicial incorporada não encontrada.");
            using var reader = new StreamReader(stream);
            Execute(connection, transaction, reader.ReadToEnd());
        }
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public T Read<T>(Func<PdvState, T> action)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = action(Load(connection, transaction));
        transaction.Commit();
        return result;
    }

    public T Write<T>(Func<PdvState, T> action)
    {
        using var connection = Open();
        // BEGIN IMMEDIATE prevents stale read/modify/write races across processes and instances.
        using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            var state = Load(connection, transaction);
            var previousProducts = state.Products.ToDictionary(p => p.Id);
            var previousSessions = state.Sessions.ToDictionary(x => x.Session.Id);
            var previousSales = state.Sales.ToDictionary(x => x.Sale.Id);
            var stockCount = state.StockMovements.Count;
            var cashCount = state.CashMovements.Count;
            var auditCount = state.Audit.Count;
            var result = action(state);
            Save(connection, transaction, state,
                state.Products.Where(p => !previousProducts.TryGetValue(p.Id, out var old) || old != p),
                state.Sessions.Where(x => !previousSessions.TryGetValue(x.Session.Id, out var old) || old != x),
                state.Sales.Where(x => !previousSales.TryGetValue(x.Sale.Id, out var old) || old != x),
                stockCount, cashCount, auditCount);
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new DomainException("Operação viola uma restrição do banco de dados (SKU único ou integridade).");
        }
    }

    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction t, string sql, params object?[] values)
    {
        var command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
            command.Parameters.AddWithValue($"@p{i}", values[i] ?? DBNull.Value);
        return command;
    }

    private static void Execute(SqliteConnection c, SqliteTransaction t, string sql, params object?[] values)
    {
        using var command = Command(c, t, sql, values);
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection c, SqliteTransaction t, string sql)
    {
        using var command = Command(c, t, sql);
        return command.ExecuteScalar();
    }

    private static void Query(SqliteConnection c, SqliteTransaction t, string sql, Action<SqliteDataReader> action)
    {
        using var command = Command(c, t, sql);
        using var reader = command.ExecuteReader();
        while (reader.Read()) action(reader);
    }

    private static DateTime Date(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
    private static string Stamp(DateTime date) => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static decimal Amount(SqliteDataReader r, int index) => Money.FromCents(r.GetInt64(index));

    private static PdvState Load(SqliteConnection c, SqliteTransaction t)
    {
        var state = new PdvState();
        Query(c, t, "SELECT id,sku,name,category,price_cents,cost_cents,active,stock FROM products ORDER BY id", r =>
            state.Products.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                Amount(r, 4), r.IsDBNull(5) ? null : Amount(r, 5), r.GetBoolean(6), r.GetInt32(7))));
        Query(c, t, "SELECT id,operator,opened_at,opening_cents,closed_at,counted_cents FROM cash_sessions ORDER BY id", r =>
            state.Sessions.Add(new(new(r.GetInt64(0), r.GetString(1), Date(r.GetString(2)), Amount(r, 3),
                r.IsDBNull(4) ? null : Date(r.GetString(4))), r.IsDBNull(5) ? null : Amount(r, 5))));
        var items = new Dictionary<long, List<SaleItem>>();
        Query(c, t, "SELECT sale_id,product_id,sku,name,quantity,unit_price_cents FROM sale_items ORDER BY sale_id,product_id", r =>
        {
            if (!items.TryGetValue(r.GetInt64(0), out var list)) items[r.GetInt64(0)] = list = [];
            list.Add(new(r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt32(4), Amount(r, 5)));
        });
        var payments = new Dictionary<long, List<Payment>>();
        Query(c, t, "SELECT sale_id,method,amount_cents FROM payments ORDER BY sale_id,sequence", r =>
        {
            if (!payments.TryGetValue(r.GetInt64(0), out var list)) payments[r.GetInt64(0)] = list = [];
            list.Add(new((PaymentMethod)r.GetInt32(1), Amount(r, 2)));
        });
        Query(c, t, "SELECT id,cash_session_id,operator,created_at,status,discount_cents,change_cents FROM sales ORDER BY id", r =>
        {
            var id = r.GetInt64(0);
            state.Sales.Add(new(new(id, r.GetInt64(1), r.GetString(2), Date(r.GetString(3)),
                (SaleStatus)r.GetInt32(4), items.TryGetValue(id, out var saleItems) ? saleItems.ToArray() : [],
                Amount(r, 5), Amount(r, 6)), payments.TryGetValue(id, out var salePayments) ? salePayments.ToArray() : []));
        });
        Query(c, t, "SELECT id,product_id,product_name,type,quantity,reason,operator,created_at FROM stock_movements ORDER BY id", r =>
            state.StockMovements.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetString(2), (StockMovementType)r.GetInt32(3),
                r.GetInt32(4), r.GetString(5), r.GetString(6), Date(r.GetString(7)))));
        Query(c, t, "SELECT id,session_id,type,amount_cents,reason,operator,created_at FROM cash_movements ORDER BY id", r =>
            state.CashMovements.Add(new(r.GetInt64(0), r.GetInt64(1), (CashMovementType)r.GetInt32(2),
                Amount(r, 3), r.GetString(4), r.GetString(5), Date(r.GetString(6)))));
        Query(c, t, "SELECT id,entity,entity_id,action,operator,created_at,details FROM audit ORDER BY id", r =>
            state.Audit.Add(new(r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetString(3),
                r.GetString(4), Date(r.GetString(5)), r.GetString(6))));
        return state;
    }

    private static void Save(SqliteConnection c, SqliteTransaction t, PdvState state,
        IEnumerable<Product> products, IEnumerable<StoredSession> sessions, IEnumerable<StoredSale> sales,
        int stockCount, int cashCount, int auditCount)
    {
        foreach (var p in products)
            Execute(c, t, """
                INSERT INTO products(id,sku,name,category,price_cents,cost_cents,active,stock)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)
                ON CONFLICT(id) DO UPDATE SET sku=excluded.sku,name=excluded.name,category=excluded.category,
                price_cents=excluded.price_cents,cost_cents=excluded.cost_cents,active=excluded.active,stock=excluded.stock
                """, p.Id, p.Sku, p.Name, p.Category, Money.ToCents(p.Price),
                p.Cost.HasValue ? Money.ToCents(p.Cost.Value) : null, p.Active, p.Stock);
        foreach (var x in sessions)
            Execute(c, t, """
                INSERT INTO cash_sessions(id,operator,opened_at,opening_cents,closed_at,counted_cents)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5)
                ON CONFLICT(id) DO UPDATE SET closed_at=excluded.closed_at,counted_cents=excluded.counted_cents
                """, x.Session.Id, x.Session.Operator, Stamp(x.Session.OpenedAt), Money.ToCents(x.Session.OpeningAmount),
                x.Session.ClosedAt.HasValue ? Stamp(x.Session.ClosedAt.Value) : null,
                x.CountedCash.HasValue ? Money.ToCents(x.CountedCash.Value) : null);
        foreach (var x in sales)
        {
            var sale = x.Sale;
            Execute(c, t, """
                INSERT INTO sales(id,cash_session_id,operator,created_at,status,discount_cents,change_cents)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)
                ON CONFLICT(id) DO UPDATE SET status=excluded.status,discount_cents=excluded.discount_cents,change_cents=excluded.change_cents
                """, sale.Id, sale.CashSessionId, sale.Operator, Stamp(sale.CreatedAt), (int)sale.Status,
                Money.ToCents(sale.Discount), Money.ToCents(sale.Change));
            Execute(c, t, "DELETE FROM sale_items WHERE sale_id=@p0", sale.Id);
            foreach (var item in sale.Items)
                Execute(c, t, """
                    INSERT INTO sale_items(sale_id,product_id,sku,name,quantity,unit_price_cents)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5)
                    """, sale.Id, item.ProductId, item.Sku, item.Name, item.Quantity, Money.ToCents(item.UnitPrice));
            Execute(c, t, "DELETE FROM payments WHERE sale_id=@p0", sale.Id);
            for (var i = 0; i < x.Payments.Count; i++)
                Execute(c, t, "INSERT INTO payments(sale_id,sequence,method,amount_cents) VALUES(@p0,@p1,@p2,@p3)",
                    sale.Id, i, (int)x.Payments[i].Method, Money.ToCents(x.Payments[i].Amount));
        }
        foreach (var m in state.StockMovements.Skip(stockCount))
            Execute(c, t, """
                INSERT INTO stock_movements(id,product_id,product_name,type,quantity,reason,operator,created_at)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7) ON CONFLICT(id) DO NOTHING
                """, m.Id, m.ProductId, m.ProductName, (int)m.Type, m.Quantity, m.Reason, m.Operator, Stamp(m.CreatedAt));
        foreach (var m in state.CashMovements.Skip(cashCount))
            Execute(c, t, """
                INSERT INTO cash_movements(id,session_id,type,amount_cents,reason,operator,created_at)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6) ON CONFLICT(id) DO NOTHING
                """, m.Id, m.SessionId, (int)m.Type, Money.ToCents(m.Amount), m.Reason, m.Operator, Stamp(m.CreatedAt));
        foreach (var a in state.Audit.Skip(auditCount))
            Execute(c, t, """
                INSERT INTO audit(id,entity,entity_id,action,operator,created_at,details)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6) ON CONFLICT(id) DO NOTHING
                """, a.Id, a.Entity, a.EntityId, a.Action, a.Operator, Stamp(a.CreatedAt), a.Details);
    }
}
