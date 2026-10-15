using ChurrascariaPDV.Application;
using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.Infrastructure;

public sealed class SqlitePdvService : IPdvService
{
    private readonly PdvService service;
    public SqlitePdvService(string databasePath, bool allowNegativeStock = false)
        => service = new(new SqlitePdvStore(databasePath), allowNegativeStock);

    public IReadOnlyList<Product> GetProducts() => service.GetProducts();
    public Product SaveProduct(long? id, ProductInput input, string operatorName) => service.SaveProduct(id, input, operatorName);
    public void DeleteProduct(long id, string operatorName) => service.DeleteProduct(id, operatorName);
    public void MoveStock(long productId, StockMovementType type, int quantity, string reason, string operatorName)
        => service.MoveStock(productId, type, quantity, reason, operatorName);
    public IReadOnlyList<StockMovement> GetStockMovements(long? productId = null) => service.GetStockMovements(productId);
    public CashSession? GetOpenCashSession() => service.GetOpenCashSession();
    public CashSession OpenCash(decimal openingAmount, string operatorName) => service.OpenCash(openingAmount, operatorName);
    public void MoveCash(CashMovementType type, decimal amount, string reason, string operatorName)
        => service.MoveCash(type, amount, reason, operatorName);
    public CashSummary GetCashSummary(long sessionId) => service.GetCashSummary(sessionId);
    public CashSummary CloseCash(decimal countedCash, string operatorName) => service.CloseCash(countedCash, operatorName);
    public Sale CreateSale(string operatorName) => service.CreateSale(operatorName);
    public Sale GetSale(long saleId) => service.GetSale(saleId);
    public IReadOnlyList<SaleReport> GetOpenSales() => service.GetOpenSales();
    public Sale SetItemQuantity(long saleId, long productId, int quantity, string operatorName)
        => service.SetItemQuantity(saleId, productId, quantity, operatorName);
    public Sale SetDiscount(long saleId, decimal discount, string operatorName) => service.SetDiscount(saleId, discount, operatorName);
    public Sale CompleteSale(long saleId, IReadOnlyList<Payment> payments, string operatorName)
        => service.CompleteSale(saleId, payments, operatorName);
    public void CancelSale(long saleId, string reason, string operatorName) => service.CancelSale(saleId, reason, operatorName);
    public IReadOnlyList<SaleReport> GetSales(DateTime fromUtc, DateTime toUtc) => service.GetSales(fromUtc, toUtc);
    public IReadOnlyList<BestSeller> GetBestSellers(DateTime fromUtc, DateTime toUtc) => service.GetBestSellers(fromUtc, toUtc);
}
