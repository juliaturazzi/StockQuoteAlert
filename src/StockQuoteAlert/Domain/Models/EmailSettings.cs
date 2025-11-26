namespace StockQuoteAlert.Domain.Models;

public class EmailSettings
{
    public string? SenderEmail { get; set; }
    public string? SmtpServer { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? RecipientEmail { get; set; }
    
    public bool IsAlertCooldownEnabled { get; set; } = true;
    public int AlertCooldownSeconds { get; set; } = 300;

    public bool IsConfigured => 
        !string.IsNullOrWhiteSpace(SmtpServer) &&
        !string.IsNullOrWhiteSpace(SmtpUser) &&
        !string.IsNullOrWhiteSpace(SmtpPass) &&
        !string.IsNullOrWhiteSpace(SenderEmail) &&
        !string.IsNullOrWhiteSpace(RecipientEmail) &&
        SmtpPort > 0;

    public bool IsValid() => IsConfigured;
}