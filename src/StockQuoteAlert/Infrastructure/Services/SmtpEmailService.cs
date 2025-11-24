using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Infrastructure.Services;

public class SmtpEmailService(EmailSettings settings, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = settings;
    private readonly ILogger<SmtpEmailService> _logger = logger;

    public async Task SendEmailAsync(string subject, string body)
    {
        try
        {
            var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SmtpPass),
                UseDefaultCredentials = false,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SmtpUser),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            
            mailMessage.To.Add(_settings.RecipientEmail);

            await client.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Email successfully sent to {email}.", _settings.RecipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to send email to {email}.", _settings.RecipientEmail);
        }
    }
}