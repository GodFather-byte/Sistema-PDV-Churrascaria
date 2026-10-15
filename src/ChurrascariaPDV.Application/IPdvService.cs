using ChurrascariaPDV.Domain;

namespace ChurrascariaPDV.Application;

public interface IPdvService
{
    IReadOnlyList<Product> GetProducts();
    Product SaveProduct(long? id, ProductInput input, string operatorName);
    void DeleteProduct(long id, string operatorName);
    void MoveStock(long productId, StockMovementType type, int quantity, string reason, string operatorName);
    IReadOnlyList<StockMovement> GetStockMovements(long? productId = null);
    CashSession? GetOpenCashSession();
    CashSession OpenCash(decimal openingAmount, string operatorName);
    void MoveCash(CashMovementType type, decimal amount, string reason, string operatorName);
    CashSummary GetCashSummary(long sessionId);
    CashSummary CloseCash(decimal countedCash, string operatorName);
    Sale CreateSale(string operatorName);
    Sale GetSale(long saleId);
    IReadOnlyList<SaleReport> GetOpenSales();
    Sale SetItemQuantity(long saleId, long productId, int quantity, string operatorName);
    Sale SetDiscount(long saleId, decimal discount, string operatorName);
    Sale CompleteSale(long saleId, IReadOnlyList<Payment> payments, string operatorName);
    void CancelSale(long saleId, string reason, string operatorName);
    IReadOnlyList<SaleReport> GetSales(DateTime fromUtc, DateTime toUtc);
    IReadOnlyList<BestSeller> GetBestSellers(DateTime fromUtc, DateTime toUtc);
}
