namespace StockQuoteAlert.Domain.Models
{
    public class StockConfiguration(string ticker, decimal sellingPrice, decimal buyingPrice)
    {
        public string Ticker { get; set; } = ticker;
        public decimal SellingPrice { get; set; } = sellingPrice;
        public decimal BuyingPrice { get; set; } = buyingPrice;
    }
}