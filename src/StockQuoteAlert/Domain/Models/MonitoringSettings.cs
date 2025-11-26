namespace StockQuoteAlert.Domain.Models;

public class MonitoringSettings
{
    public int CheckIntervalMinutes { get; set; }
    public required string ApiBaseUrl { get; set; }
    public string? BrapiToken { get; set; }
}
