using System.Net;
using System.Net.Mail;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Infrastructure.Services;

public class SmtpEmailService(EmailSettings settings) : IEmailService
{
    private readonly EmailSettings _settings = settings;

    public async Task SendEmailAsync(string subject, string body)
    {
        if (!_settings.IsConfigured)
        {
            return;
        }

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail!, "Stock Quote Alert"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };

        mailMessage.To.Add(_settings.RecipientEmail!);
        using var smtpClient = new SmtpClient(_settings.SmtpServer!, _settings.SmtpPort.GetValueOrDefault())
        {
            Credentials = new NetworkCredential(_settings.SmtpUser!, _settings.SmtpPass!),
            EnableSsl = true
        };

        await smtpClient.SendMailAsync(mailMessage);
    }
}