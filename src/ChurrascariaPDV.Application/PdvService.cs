using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.Application;

public sealed class PdvService(IPdvStore store, bool allowNegativeStock = false) : IPdvService
{
    private static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} é obrigatório.");
        return value.Trim();
    }

    private static void Audit(PdvState state, string entity, long id, string action, string who, string details = "")
        => state.Audit.Add(new(state.Audit.Count == 0 ? 1 : state.Audit.Max(a => a.Id) + 1,
            entity, id, action, Required(who, "Operador"), DateTime.UtcNow, details));

    private static Product ProductById(PdvState s, long id)
        => s.Products.SingleOrDefault(p => p.Id == id) ?? throw new DomainException("Produto não encontrado.");
    private static StoredSale SaleById(PdvState s, long id)
        => s.Sales.SingleOrDefault(p => p.Sale.Id == id) ?? throw new DomainException("Venda não encontrada.");
    private static CashSession OpenSession(PdvState s)
        => s.Sessions.SingleOrDefault(c => c.Session.ClosedAt == null)?.Session
            ?? throw new DomainException("Abra o caixa antes de continuar.");
    private static Sale EditableSale(PdvState s, long id)
    {
        var sale = SaleById(s, id).Sale;
        if (sale.Status != SaleStatus.Open)
            throw new DomainException("Somente vendas abertas podem ser alteradas.");
        if (OpenSession(s).Id != sale.CashSessionId)
            throw new DomainException("O caixa de origem da venda está fechado.");
        return sale;
    }

    private static void ReplaceSale(PdvState s, Sale sale, IReadOnlyList<Payment>? payments = null)
    {
        var index = s.Sales.FindIndex(x => x.Sale.Id == sale.Id);
        s.Sales[index] = new(sale, payments ?? s.Sales[index].Payments);
    }

    public IReadOnlyList<Product> GetProducts() => store.Read(s => s.Products.OrderBy(p => p.Name).ToArray());

    public Product SaveProduct(long? id, ProductInput input, string operatorName) => store.Write(s =>
    {
        ArgumentNullException.ThrowIfNull(input);
        var sku = Required(input.Sku, "SKU");
        var name = Required(input.Name, "Nome");
        var category = Required(input.Category, "Categoria");
        Money.Validate(input.Price, "Preço", positive: true);
        if (input.Cost.HasValue) Money.Validate(input.Cost.Value, "Custo");
        if (s.Products.Any(p => p.Id != id && p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("SKU já cadastrado.");
        var previous = id.HasValue ? ProductById(s, id.Value) : null;
        var product = new Product(previous?.Id ?? (s.Products.Count == 0 ? 1 : s.Products.Max(p => p.Id) + 1),
            sku, name, category, input.Price, input.Cost, input.Active, previous?.Stock ?? 0);
        if (previous != null) s.Products.Remove(previous);
        s.Products.Add(product);
        Audit(s, "Product", product.Id, previous == null ? "Created" : "Updated", operatorName,
            $"SKU={sku}; preço={input.Price}; ativo={input.Active}");
        return product;
    });

    public void DeleteProduct(long id, string operatorName) => store.Write(s =>
    {
        var product = ProductById(s, id);
        s.Products[s.Products.IndexOf(product)] = product with { Active = false };
        Audit(s, "Product", id, "Deactivated", operatorName);
        return true;
    });

    private void ChangeStock(PdvState s, long id, StockMovementType type, int quantity, string reason, string who)
    {
        if (!Enum.IsDefined(type) || quantity == 0 || (quantity < 0 && type != StockMovementType.Adjustment))
            throw new DomainException("Movimento inválido: entradas e saídas devem ser positivas; ajuste é uma variação não zero com sinal.");
        reason = Required(reason, "Motivo");
        var p = ProductById(s, id);
        var stock = (long)p.Stock + (type == StockMovementType.Exit ? -(long)quantity : quantity);
        if (stock > int.MaxValue || stock < int.MinValue)
            throw new DomainException("Quantidade excede o limite de estoque.");
        if (!allowNegativeStock && stock < 0)
            throw new DomainException($"Estoque insuficiente para {p.Name}.");
        s.Products[s.Products.IndexOf(p)] = p with { Stock = (int)stock };
        var movementId = s.StockMovements.Count == 0 ? 1 : s.StockMovements.Max(m => m.Id) + 1;
        s.StockMovements.Add(new(movementId, id, p.Name, type, quantity, reason, Required(who, "Operador"), DateTime.UtcNow));
        Audit(s, "Stock", id, type.ToString(), who, $"{quantity}: {reason}");
    }

    public void MoveStock(long productId, StockMovementType type, int quantity, string reason, string operatorName)
        => store.Write(s => { ChangeStock(s, productId, type, quantity, reason, operatorName); return true; });

    public IReadOnlyList<StockMovement> GetStockMovements(long? productId = null)
        => store.Read(s => s.StockMovements.Where(m => productId == null || m.ProductId == productId)
            .OrderByDescending(m => m.Id).ToArray());

    public CashSession? GetOpenCashSession()
        => store.Read(s => s.Sessions.SingleOrDefault(c => c.Session.ClosedAt == null)?.Session);

    public CashSession OpenCash(decimal openingAmount, string operatorName) => store.Write(s =>
    {
        Money.Validate(openingAmount, "Fundo inicial");
        if (s.Sessions.Any(c => c.Session.ClosedAt == null))
            throw new DomainException("Já existe um caixa aberto.");
        var session = new CashSession(s.Sessions.Count == 0 ? 1 : s.Sessions.Max(c => c.Session.Id) + 1,
            Required(operatorName, "Operador"), DateTime.UtcNow, openingAmount, null);
        s.Sessions.Add(new(session, null));
        Audit(s, "CashSession", session.Id, "Opened", operatorName);
        return session;
    });

    private static CashSummary Summary(PdvState s, long sessionId)
    {
        var session = s.Sessions.SingleOrDefault(c => c.Session.Id == sessionId)
            ?? throw new DomainException("Caixa não encontrado.");
        var sales = s.Sales.Where(x => x.Sale.CashSessionId == sessionId && x.Sale.Status == SaleStatus.Completed).ToArray();
        var byMethod = Enum.GetValues<PaymentMethod>().ToDictionary(m => m,
            m => sales.Sum(x => x.Payments.Where(p => p.Method == m).Sum(p => p.Amount)
                - (m == PaymentMethod.Cash ? x.Sale.Change : 0)));
        var supplies = s.CashMovements.Where(m => m.SessionId == sessionId && m.Type == CashMovementType.Supply).Sum(m => m.Amount);
        var withdrawals = s.CashMovements.Where(m => m.SessionId == sessionId && m.Type == CashMovementType.Withdrawal).Sum(m => m.Amount);
        var expected = session.Session.OpeningAmount + byMethod[PaymentMethod.Cash] + supplies - withdrawals;
        return new(sessionId, session.Session.OpeningAmount, byMethod, supplies, withdrawals,
            expected, session.CountedCash, session.CountedCash - expected);
    }

    public CashSummary GetCashSummary(long sessionId) => store.Read(s => Summary(s, sessionId));

    public void MoveCash(CashMovementType type, decimal amount, string reason, string operatorName) => store.Write(s =>
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Movimento de caixa inválido.");
        Money.Validate(amount, "Valor", positive: true);
        reason = Required(reason, "Motivo");
        var session = OpenSession(s);
        if (type == CashMovementType.Withdrawal && Summary(s, session.Id).ExpectedCash < amount)
            throw new DomainException("Saldo em dinheiro insuficiente para sangria.");
        s.CashMovements.Add(new(s.CashMovements.Count == 0 ? 1 : s.CashMovements.Max(m => m.Id) + 1,
            session.Id, type, amount, reason, Required(operatorName, "Operador"), DateTime.UtcNow));
        Audit(s, "CashSession", session.Id, type.ToString(), operatorName, $"{amount}: {reason}");
        return true;
    });

    public CashSummary CloseCash(decimal countedCash, string operatorName) => store.Write(s =>
    {
        Money.Validate(countedCash, "Valor contado");
        var session = OpenSession(s);
        if (s.Sales.Any(x => x.Sale.CashSessionId == session.Id && x.Sale.Status == SaleStatus.Open))
            throw new DomainException("Finalize ou cancele as vendas abertas antes de fechar o caixa.");
        var index = s.Sessions.FindIndex(x => x.Session.Id == session.Id);
        s.Sessions[index] = new(session with { ClosedAt = DateTime.UtcNow }, countedCash);
        Audit(s, "CashSession", session.Id, "Closed", operatorName, $"Contado={countedCash}");
        return Summary(s, session.Id);
    });

    public Sale CreateSale(string operatorName) => store.Write(s =>
    {
        var session = OpenSession(s);
        var sale = new Sale(s.Sales.Count == 0 ? 1 : s.Sales.Max(x => x.Sale.Id) + 1, session.Id,
            Required(operatorName, "Operador"), DateTime.UtcNow, SaleStatus.Open, [], 0, 0);
        s.Sales.Add(new(sale, []));
        Audit(s, "Sale", sale.Id, "Created", operatorName);
        return sale;
    });

    public Sale GetSale(long saleId) => store.Read(s => SaleById(s, saleId).Sale);
    private static SaleReport Report(Sale sale)
        => new(sale.Id, sale.CreatedAt, sale.Operator, sale.Status, sale.Subtotal, sale.Discount, sale.Total);
    public IReadOnlyList<SaleReport> GetOpenSales()
        => store.Read(s => s.Sales.Where(x => x.Sale.Status == SaleStatus.Open).Select(x => Report(x.Sale)).ToArray());

    public Sale SetItemQuantity(long saleId, long productId, int quantity, string operatorName) => store.Write(s =>
    {
        var sale = EditableSale(s, saleId);
        if (quantity < 0) throw new DomainException("Quantidade não pode ser negativa.");
        var items = sale.Items.ToList();
        var existing = items.SingleOrDefault(i => i.ProductId == productId);
        if (quantity > 0)
        {
            var product = ProductById(s, productId);
            if (!product.Active) throw new DomainException("Produto inativo não pode ser vendido.");
            var item = existing == null
                ? new SaleItem(product.Id, product.Sku, product.Name, quantity, product.Price)
                : existing with { Quantity = quantity };
            if (existing != null) items.Remove(existing);
            items.Add(item);
        }
        else if (existing != null) items.Remove(existing);
        SaleRules.Total(items, sale.Discount);
        sale = sale with { Items = items.ToArray() };
        ReplaceSale(s, sale);
        Audit(s, "Sale", saleId, "ItemChanged", operatorName, $"Produto={productId}; quantidade={quantity}");
        return sale;
    });

    public Sale SetDiscount(long saleId, decimal discount, string operatorName) => store.Write(s =>
    {
        var sale = EditableSale(s, saleId);
        SaleRules.Total(sale.Items, discount);
        sale = sale with { Discount = discount };
        ReplaceSale(s, sale);
        Audit(s, "Sale", saleId, "DiscountChanged", operatorName, discount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return sale;
    });

    public Sale CompleteSale(long saleId, IReadOnlyList<Payment> payments, string operatorName) => store.Write(s =>
    {
        ArgumentNullException.ThrowIfNull(payments);
        var sale = EditableSale(s, saleId);
        if (sale.Items.Count == 0) throw new DomainException("Não é possível finalizar venda vazia.");
        var totals = SaleRules.CalculatePayments(SaleRules.Total(sale.Items, sale.Discount), payments);
        foreach (var item in sale.Items)
        {
            if (!ProductById(s, item.ProductId).Active) throw new DomainException($"Produto {item.Name} está inativo.");
            ChangeStock(s, item.ProductId, StockMovementType.Exit, item.Quantity, $"Venda #{sale.Id}", operatorName);
        }
        sale = sale with { Status = SaleStatus.Completed, Change = totals.Change };
        ReplaceSale(s, sale, payments.ToArray());
        foreach (var payment in payments)
            Audit(s, "Sale", saleId, "Payment", operatorName, $"{payment.Method}: {payment.Amount}");
        Audit(s, "Sale", saleId, "Completed", operatorName);
        return sale;
    });

    public void CancelSale(long saleId, string reason, string operatorName) => store.Write(s =>
    {
        reason = Required(reason, "Motivo");
        var stored = SaleById(s, saleId);
        var sale = stored.Sale;
        if (sale.Status == SaleStatus.Cancelled) throw new DomainException("Venda já cancelada.");
        if (sale.Status == SaleStatus.Completed)
        {
            if (!s.Sessions.Any(c => c.Session.Id == sale.CashSessionId && c.Session.ClosedAt == null))
                throw new DomainException("Não é possível cancelar: o caixa de origem da venda está fechado.");
            var refund = stored.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) - sale.Change;
            if (Summary(s, sale.CashSessionId).ExpectedCash < refund)
                throw new DomainException("Saldo em dinheiro insuficiente para devolver o pagamento.");
            foreach (var item in sale.Items)
                ChangeStock(s, item.ProductId, StockMovementType.Entry, item.Quantity, $"Cancelamento venda #{sale.Id}: {reason}", operatorName);
            Audit(s, "Sale", saleId, "PaymentsReversed", operatorName, reason);
        }
        ReplaceSale(s, sale with { Status = SaleStatus.Cancelled });
        Audit(s, "Sale", saleId, "Cancelled", operatorName, reason);
        return true;
    });

    private static void Period(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || fromUtc >= toUtc)
            throw new DomainException("Informe um período UTC válido (início inclusivo e fim exclusivo).");
    }

    public IReadOnlyList<SaleReport> GetSales(DateTime fromUtc, DateTime toUtc)
    {
        Period(fromUtc, toUtc);
        return store.Read(s => s.Sales.Where(x => x.Sale.CreatedAt >= fromUtc && x.Sale.CreatedAt < toUtc)
            .OrderByDescending(x => x.Sale.Id).Select(x => Report(x.Sale)).ToArray());
    }

    public IReadOnlyList<BestSeller> GetBestSellers(DateTime fromUtc, DateTime toUtc)
    {
        Period(fromUtc, toUtc);
        return store.Read(s => s.Sales.Where(x => x.Sale.Status == SaleStatus.Completed &&
                x.Sale.CreatedAt >= fromUtc && x.Sale.CreatedAt < toUtc)
            .SelectMany(x => x.Sale.Items).GroupBy(i => i.ProductId)
            .Select(g => new BestSeller(g.Key, g.Last().Name, g.Sum(i => i.Quantity), g.Sum(i => i.Subtotal)))
            .OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId).ToArray());
    }
}
