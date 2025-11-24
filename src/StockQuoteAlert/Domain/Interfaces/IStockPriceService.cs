namespace StockQuoteAlert.Domain.Interfaces
{
    public interface IStockPriceService
    {
        Task<decimal?> GetPriceAsync(string symbol);
    }
}