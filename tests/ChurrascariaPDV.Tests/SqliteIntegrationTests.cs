using ChurrascariaPDV.Domain;
using ChurrascariaPDV.Infrastructure;
using Microsoft.Data.Sqlite;

namespace ChurrascariaPDV.Tests;

public sealed class SqliteIntegrationTests : IDisposable
{
    private readonly string directory = Path.Combine(Directory.GetCurrentDirectory(), ".test-data", Guid.NewGuid().ToString("N"));
    private readonly string path;
    private readonly SqlitePdvService service;
    private const string Operator = "Operador teste";

    public SqliteIntegrationTests()
    {
        path = Path.Combine(directory, "pdv.sqlite");
        service = new(path);
    }

    private Product Product(string sku = "P001", decimal price = 10m, int stock = 10)
    {
        var product = service.SaveProduct(null, new(sku, $"Produto {sku}", "Churrasco", price, 4.10m, true), Operator);
        if (stock > 0) service.MoveStock(product.Id, StockMovementType.Entry, stock, "Compra inicial", Operator);
        return product;
    }

    private Sale Draft(Product product, int quantity = 1)
    {
        if (service.GetOpenCashSession() == null) service.OpenCash(100, Operator);
        var sale = service.CreateSale(Operator);
        return service.SetItemQuantity(sale.Id, product.Id, quantity, Operator);
    }

    private SqliteConnection Connection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, ForeignKeys = true, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private object Scalar(string sql)
    {
        using var connection = Connection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    [Fact]
    public void EndToEndMixedPaymentPersistsDraftAndCashCloseReopen()
    {
        var product = Product(price: 25.50m);
        var sale = Draft(product, 2);
        service.SetDiscount(sale.Id, 1m, Operator);
        var reopened = new SqlitePdvService(path);
        Assert.Single(reopened.GetOpenSales());
        Assert.Equal(50m, reopened.GetSale(sale.Id).Total);
        sale = reopened.CompleteSale(sale.Id, [new(PaymentMethod.Pix, 20m), new(PaymentMethod.Cash, 40m)], Operator);
        Assert.Equal(10m, sale.Change);
        Assert.Equal(8, reopened.GetProducts().Single().Stock);
        reopened.MoveCash(CashMovementType.Supply, 5m, "Troco", Operator);
        reopened.MoveCash(CashMovementType.Withdrawal, 15m, "Sangria", Operator);
        var summary = reopened.GetCashSummary(sale.CashSessionId);
        Assert.Equal(30m, summary.SalesByMethod[PaymentMethod.Cash]);
        Assert.Equal(20m, summary.SalesByMethod[PaymentMethod.Pix]);
        Assert.Equal(120m, summary.ExpectedCash);
        summary = reopened.CloseCash(119.50m, Operator);
        Assert.Equal(-.50m, summary.Difference);
        Assert.Null(reopened.GetOpenCashSession());
        var next = reopened.OpenCash(10, Operator);
        Assert.NotEqual(sale.CashSessionId, next.Id);
        Assert.Equal(10m, reopened.GetCashSummary(next.Id).ExpectedCash);
        Assert.Equal(120m, new SqlitePdvService(path).GetCashSummary(sale.CashSessionId).ExpectedCash);
    }

    [Fact]
    public void CompletionFailureRollsBackAllStockPaymentsAndAudit()
    {
        var first = Product("FIRST", stock: 10);
        var second = Product("SECOND", stock: 0);
        var sale = Draft(first, 2);
        service.SetItemQuantity(sale.Id, second.Id, 1, Operator);
        var auditCount = Scalar("SELECT count(*) FROM audit");
        var movementCount = service.GetStockMovements().Count;
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 30)], Operator));
        Assert.Equal(10, service.GetProducts().Single(p => p.Id == first.Id).Stock);
        Assert.Equal(SaleStatus.Open, service.GetSale(sale.Id).Status);
        Assert.Equal(movementCount, service.GetStockMovements().Count);
        Assert.Equal(auditCount, Scalar("SELECT count(*) FROM audit"));
        Assert.Equal(0L, Scalar("SELECT count(*) FROM payments"));
        Assert.Equal(100m, service.GetCashSummary(sale.CashSessionId).ExpectedCash);
    }

    [Fact]
    public void InactiveProductsCannotBeAddedOrCheckedOutButCanBeRemoved()
    {
        var p = Product();
        var sale = Draft(p);
        service.DeleteProduct(p.Id, Operator);
        Assert.Throws<DomainException>(() => service.SetItemQuantity(sale.Id, p.Id, 2, Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 10)], Operator));
        Assert.Empty(service.SetItemQuantity(sale.Id, p.Id, 0, Operator).Items);
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 10)], Operator));
        Assert.Equal(10, service.GetProducts().Single().Stock);
        Assert.False(service.GetProducts().Single().Active);
    }

    [Fact]
    public void SnapshotPriceSurvivesProductEditsAndDraftEditsNeverMoveStock()
    {
        var p = Product();
        var sale = Draft(p);
        service.SaveProduct(p.Id, new("P001", "Renomeado", "Nova categoria", 20m, null, true), Operator);
        sale = service.SetItemQuantity(sale.Id, p.Id, 2, Operator);
        Assert.Equal(10m, sale.Items.Single().UnitPrice);
        Assert.Equal(p.Name, sale.Items.Single().Name);
        Assert.Equal(10, service.GetProducts().Single().Stock);
        sale = service.SetItemQuantity(sale.Id, p.Id, 0, Operator);
        Assert.Empty(sale.Items);
        sale = service.SetItemQuantity(sale.Id, p.Id, 1, Operator);
        Assert.Equal(20m, sale.Total);
        service.CancelSale(sale.Id, "Cliente desistiu", Operator);
        Assert.Equal(10, service.GetProducts().Single().Stock);
        Assert.Single(service.GetStockMovements());
    }

    [Fact]
    public void CompletedCancellationRestoresExactlyOnceAndReversesEffectivePayments()
    {
        var p = Product();
        var sale = Draft(p, 2);
        service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 30)], Operator);
        service.CancelSale(sale.Id, "Estorno", Operator);
        Assert.Equal(SaleStatus.Cancelled, service.GetSale(sale.Id).Status);
        Assert.Equal(10, service.GetProducts().Single().Stock);
        Assert.Equal(100m, service.GetCashSummary(sale.CashSessionId).ExpectedCash);
        Assert.All(service.GetCashSummary(sale.CashSessionId).SalesByMethod.Values, value => Assert.Equal(0, value));
        Assert.Throws<DomainException>(() => service.CancelSale(sale.Id, "Novamente", Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 20)], Operator));
        Assert.Equal(10, service.GetProducts().Single().Stock);
        Assert.Equal(3, service.GetStockMovements().Count);
        Assert.Equal(1L, Scalar("SELECT count(*) FROM audit WHERE action='PaymentsReversed'"));
        Assert.Equal(1L, Scalar("SELECT count(*) FROM payments"));
    }

    [Fact]
    public void CompletedCancellationRejectedAfterOriginatingCashClosedEvenIfAnotherIsOpen()
    {
        var p = Product();
        var sale = Draft(p);
        service.CompleteSale(sale.Id, [new(PaymentMethod.DebitCard, 10)], Operator);
        service.CloseCash(100, Operator);
        service.OpenCash(100, Operator);
        var error = Assert.Throws<DomainException>(() => service.CancelSale(sale.Id, "Estorno", Operator));
        Assert.Contains("caixa de origem", error.Message);
        Assert.Equal(SaleStatus.Completed, service.GetSale(sale.Id).Status);
        Assert.Equal(9, service.GetProducts().Single().Stock);
    }

    [Fact]
    public void InsufficientDrawerPreventsWithdrawalAndCashRefundAtomically()
    {
        var p = Product();
        var sale = Draft(p);
        service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 10)], Operator);
        Assert.Throws<DomainException>(() => service.MoveCash(CashMovementType.Withdrawal, 111, "Muito", Operator));
        service.MoveCash(CashMovementType.Withdrawal, 105, "Banco", Operator);
        var audit = Scalar("SELECT count(*) FROM audit");
        Assert.Throws<DomainException>(() => service.CancelSale(sale.Id, "Estorno", Operator));
        Assert.Equal(SaleStatus.Completed, service.GetSale(sale.Id).Status);
        Assert.Equal(9, service.GetProducts().Single().Stock);
        Assert.Equal(audit, Scalar("SELECT count(*) FROM audit"));
        service.MoveCash(CashMovementType.Supply, 5, "Reposição", Operator);
        service.CancelSale(sale.Id, "Estorno", Operator);
        Assert.Equal(0m, service.GetCashSummary(sale.CashSessionId).ExpectedCash);
    }

    [Fact]
    public void CashEnforcesOneOpenAndNoUnresolvedDrafts()
    {
        var session = service.OpenCash(0, Operator);
        Assert.Throws<DomainException>(() => new SqlitePdvService(path).OpenCash(0, Operator));
        var sale = service.CreateSale(Operator);
        Assert.Throws<DomainException>(() => service.CloseCash(0, Operator));
        Assert.Null(service.GetOpenCashSession()!.ClosedAt);
        service.CancelSale(sale.Id, "Sem itens", Operator);
        Assert.Equal(0m, service.CloseCash(0, Operator).Difference);
        Assert.Null(service.GetOpenCashSession());
        Assert.Throws<DomainException>(() => service.CreateSale(Operator));
        Assert.NotEqual(session.Id, service.OpenCash(0, Operator).Id);
    }

    [Fact]
    public void MoneyPrecisionDiscountPaymentAndQuantityValidationAreAtomic()
    {
        var p = Product();
        var sale = Draft(p);
        Assert.Throws<DomainException>(() => service.SaveProduct(null, new("X", "X", "C", 1.001m, null, true), Operator));
        Assert.Throws<DomainException>(() => service.SaveProduct(null, new("X", "X", "C", 1m, .001m, true), Operator));
        Assert.Throws<DomainException>(() => service.SetDiscount(sale.Id, .001m, Operator));
        Assert.Throws<DomainException>(() => service.SetDiscount(sale.Id, 11, Operator));
        Assert.Throws<DomainException>(() => service.SetDiscount(sale.Id, -1, Operator));
        Assert.Throws<DomainException>(() => service.SetItemQuantity(sale.Id, p.Id, -1, Operator));
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Entry, 0, "x", Operator));
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Entry, -1, "x", Operator));
        Assert.Throws<DomainException>(() => service.MoveCash(CashMovementType.Supply, .001m, "x", Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Pix, 11)], Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 9)], Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [], Operator));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new((PaymentMethod)999, 10)], Operator));
        Assert.Equal(0m, service.GetSale(sale.Id).Discount);
        Assert.Equal(10, service.GetProducts().Single().Stock);
        service.SetDiscount(sale.Id, 1, Operator);
        service.CompleteSale(sale.Id, [new(PaymentMethod.CreditCard, 4), new(PaymentMethod.CreditCard, 5)], Operator);
        Assert.Equal(9m, service.GetCashSummary(sale.CashSessionId).SalesByMethod[PaymentMethod.CreditCard]);
    }

    [Fact]
    public void ProductsHaveCaseInsensitiveUniqueSkuAndSoftDeletionRetainsHistory()
    {
        var p = Product("AbC");
        Assert.Throws<DomainException>(() => Product("abc"));
        var sale = Draft(p);
        service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 10)], Operator);
        service.DeleteProduct(p.Id, Operator);
        Assert.False(service.GetProducts().Single().Active);
        Assert.Single(service.GetSale(sale.Id).Items);
        Assert.Equal(2, service.GetStockMovements().Count);
        Assert.Throws<DomainException>(() => Product("ABC"));
        Assert.Equal(1L, Scalar("SELECT count(*) FROM audit WHERE action='Deactivated'"));
    }

    [Fact]
    public void AuditRequiresNonblankOperatorAcrossMutationsAndRollsBack()
    {
        Assert.Throws<DomainException>(() => service.OpenCash(10, " "));
        Assert.Null(service.GetOpenCashSession());
        Assert.Throws<DomainException>(() => service.SaveProduct(null, new("A", "A", "A", 10, null, true), " "));
        Assert.Empty(service.GetProducts());
        var p = Product();
        var sale = Draft(p);
        var count = Scalar("SELECT count(*) FROM audit");
        Assert.Throws<DomainException>(() => service.SetItemQuantity(sale.Id, p.Id, 2, " "));
        Assert.Throws<DomainException>(() => service.SetDiscount(sale.Id, 1, " "));
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Entry, 1, "x", " "));
        Assert.Throws<DomainException>(() => service.MoveCash(CashMovementType.Supply, 1, "x", " "));
        Assert.Throws<DomainException>(() => service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 10)], " "));
        Assert.Throws<DomainException>(() => service.CancelSale(sale.Id, "x", " "));
        Assert.Throws<DomainException>(() => service.DeleteProduct(p.Id, " "));
        Assert.Equal(count, Scalar("SELECT count(*) FROM audit"));
        Assert.Equal(10, service.GetProducts().Single().Stock);
        Assert.Equal(1, service.GetSale(sale.Id).Items.Single().Quantity);
        Assert.Equal(0L, Scalar("SELECT count(*) FROM audit WHERE trim(operator)='' OR substr(created_at,-1)!='Z'"));
        service.CancelSale(sale.Id, "Teste", Operator);
        Assert.Throws<DomainException>(() => service.CloseCash(100, " "));
        Assert.NotNull(service.GetOpenCashSession());
    }

    [Fact]
    public void PeriodReportsAreUtcHalfOpenAndBestSellersExcludeCancelledAndOpen()
    {
        var p = Product();
        var first = Draft(p, 2);
        service.CompleteSale(first.Id, [new(PaymentMethod.Pix, 20)], Operator);
        var cancelled = Draft(p, 3);
        service.CompleteSale(cancelled.Id, [new(PaymentMethod.Cash, 30)], Operator);
        service.CancelSale(cancelled.Id, "Estorno", Operator);
        var open = Draft(p);
        var from = DateTime.UtcNow.AddMinutes(-1);
        var to = DateTime.UtcNow.AddMinutes(1);
        var reports = service.GetSales(from, to);
        Assert.Equal(3, reports.Count);
        Assert.Contains(reports, r => r.Status == SaleStatus.Cancelled);
        var best = Assert.Single(service.GetBestSellers(from, to));
        Assert.Equal(2, best.Quantity);
        Assert.Equal(20m, best.GrossRevenue);
        Assert.Equal(2, service.GetSales(first.CreatedAt, open.CreatedAt).Count);
        Assert.DoesNotContain(service.GetSales(from, first.CreatedAt), r => r.Id == first.Id);
        Assert.Contains(service.GetSales(first.CreatedAt, to), r => r.Id == first.Id);
        Assert.Throws<DomainException>(() => service.GetSales(DateTime.Now, to));
        Assert.Throws<DomainException>(() => service.GetBestSellers(to, from));
    }

    [Fact]
    public async Task ConcurrentInstancesDoNotLoseStockUpdates()
    {
        var p = Product(stock: 0);
        var services = Enumerable.Range(0, 4).Select(_ => new SqlitePdvService(path)).ToArray();
        await Task.WhenAll(services.Select(s => Task.Run(() =>
        {
            for (var i = 0; i < 10; i++)
                s.MoveStock(p.Id, StockMovementType.Entry, 1, "Concorrência", Operator);
        })));
        Assert.Equal(40, service.GetProducts().Single().Stock);
        Assert.Equal(40, service.GetStockMovements().Count);
        Assert.Equal(40L, Scalar("SELECT count(*) FROM audit WHERE entity='Stock'"));
    }

    [Fact]
    public async Task ConcurrentCheckoutCannotOversellOrCompleteTwice()
    {
        var p = Product(stock: 1);
        var first = Draft(p);
        var second = Draft(p);
        var other = new SqlitePdvService(path);
        var results = await Task.WhenAll(new[] { (service, first.Id), (other, second.Id) }.Select(pair => Task.Run(() =>
        {
            try { pair.Item1.CompleteSale(pair.Id, [new(PaymentMethod.Pix, 10)], Operator); return true; }
            catch (DomainException) { return false; }
        })));
        Assert.Single(results, x => x);
        Assert.Equal(0, service.GetProducts().Single().Stock);
        Assert.Single(service.GetOpenSales());
        var completed = service.GetSale(first.Id).Status == SaleStatus.Completed ? first : second;
        Assert.Throws<DomainException>(() => other.CompleteSale(completed.Id, [new(PaymentMethod.Pix, 10)], Operator));
        Assert.Equal(1L, Scalar("SELECT count(*) FROM payments"));
    }

    [Fact]
    public async Task ConcurrentOpenCashAllowsExactlyOneSession()
    {
        var other = new SqlitePdvService(path);
        var results = await Task.WhenAll(new[] { service, other }.Select(s => Task.Run(() =>
        {
            try { s.OpenCash(0, Operator); return true; }
            catch (DomainException) { return false; }
        })));
        Assert.Single(results, x => x);
        Assert.Equal(1L, Scalar("SELECT count(*) FROM cash_sessions"));
    }

    [Fact]
    public void NegativeStockIsOptInAndAdjustmentsAreSignedNonzeroDeltas()
    {
        var p = Product(stock: 0);
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Exit, 1, "Saída", Operator));
        var permissive = new SqlitePdvService(path, allowNegativeStock: true);
        permissive.MoveStock(p.Id, StockMovementType.Exit, 2, "Autorizado", Operator);
        Assert.Equal(-2, service.GetProducts().Single().Stock);
        service.MoveStock(p.Id, StockMovementType.Adjustment, 5, "Contagem", Operator);
        Assert.Equal(3, service.GetProducts().Single().Stock);
        service.MoveStock(p.Id, StockMovementType.Adjustment, -3, "Contagem corrigida", Operator);
        Assert.Equal(0, service.GetProducts().Single().Stock);
        Assert.Equal(-3, service.GetStockMovements(p.Id)[0].Quantity);
        Assert.Equal(-3L, Scalar("SELECT quantity FROM stock_movements ORDER BY id DESC LIMIT 1"));
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Adjustment, 0, "Inválido", Operator));
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Adjustment, -1, "Insuficiente", Operator));
        Assert.Equal(0, service.GetProducts().Single().Stock);
        permissive.MoveStock(p.Id, StockMovementType.Adjustment, -1, "Permitido", Operator);
        Assert.Equal(-1, service.GetProducts().Single().Stock);
    }

    [Fact]
    public void ActualEmbeddedMigrationIsVersionedIdempotentAndMoneyUsesIntegers()
    {
        var assembly = typeof(SqlitePdvService).Assembly;
        using var stream = assembly.GetManifestResourceStream("ChurrascariaPDV.Infrastructure.Migrations.001_Initial.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var embedded = reader.ReadToEnd();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "src", "ChurrascariaPDV.Infrastructure", "Migrations", "001_Initial.sql")))
            root = root.Parent;
        Assert.NotNull(root);
        Assert.Equal(File.ReadAllText(Path.Combine(root!.FullName, "src", "ChurrascariaPDV.Infrastructure", "Migrations", "001_Initial.sql")), embedded);
        Assert.Equal(1L, Scalar("PRAGMA user_version"));
        Assert.Equal(1L, Scalar("PRAGMA foreign_keys"));
        var p = Product(price: 10.12m);
        var sale = Draft(p);
        service.CompleteSale(sale.Id, [new(PaymentMethod.Cash, 20)], Operator);
        new SqlitePdvService(path);
        Assert.Equal(1012L, Scalar("SELECT price_cents FROM products"));
        Assert.Equal("integer", Scalar("SELECT typeof(price_cents) FROM products"));
        Assert.Equal(988L, Scalar("SELECT change_cents FROM sales"));
        Assert.Equal(2000L, Scalar("SELECT amount_cents FROM payments"));
        Assert.Equal(0L, Scalar("SELECT count(*) FROM pragma_foreign_key_check"));
        Assert.Equal("ok", Scalar("PRAGMA integrity_check"));
    }

    [Fact]
    public void SqlInputIsParameterizedAndReasonValidationRollsBack()
    {
        var p = Product("X'); DROP TABLE products;--");
        Assert.Single(service.GetProducts());
        Assert.Throws<DomainException>(() => service.MoveStock(p.Id, StockMovementType.Exit, 1, " ", Operator));
        var sale = Draft(p);
        Assert.Throws<DomainException>(() => service.CancelSale(sale.Id, " ", Operator));
        Assert.Equal(SaleStatus.Open, service.GetSale(sale.Id).Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
