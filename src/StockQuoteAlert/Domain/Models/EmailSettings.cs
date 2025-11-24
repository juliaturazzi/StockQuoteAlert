namespace StockQuoteAlert.Domain.Models;

public class EmailSettings
{
    public required string SenderEmail { get; set; }
    public required string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public required string SmtpUser { get; set; }
    public required string SmtpPass { get; set; }
    public required string RecipientEmail { get; set; } 
}