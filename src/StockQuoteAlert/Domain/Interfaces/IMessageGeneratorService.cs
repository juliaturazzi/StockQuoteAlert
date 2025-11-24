namespace StockQuoteAlert.Domain.Interfaces;

public interface IMessageGeneratorService
{
    (string Subject, string Body) GenerateAlertMessage(
        string ticker, 
        decimal currentPrice, 
        decimal buyingPrice, 
        decimal sellingPrice);
}