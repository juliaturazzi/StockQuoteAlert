using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Workers
{
    public class StockMonitorWorker(
        ILogger<StockMonitorWorker> logger,
        IStockPriceService priceService,
        StockConfiguration configuration,
        IEmailService emailService,
        IMessageGeneratorService messageGenerator,
        EmailSettings emailSettings,
        MonitoringSettings monitoringSettings) : BackgroundService
    {
        private readonly ILogger<StockMonitorWorker> _logger = logger;
        private readonly IStockPriceService _priceService = priceService;
        private readonly StockConfiguration _configuration = configuration;
        private readonly IEmailService _emailService = emailService;
        private readonly IMessageGeneratorService _messageGenerator = messageGenerator;
        private readonly EmailSettings _emailSettings = emailSettings;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(monitoringSettings.CheckIntervalMinutes);

        private readonly Dictionary<string, DateTimeOffset> _lastAlertSent = [];

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting stock monitor for {Ticker} with buying price {BuyingPrice} and selling price {SellingPrice}",
                _configuration.Ticker, _configuration.BuyingPrice, _configuration.SellingPrice);
            
            _logger.LogInformation("Check interval set to {Minutes} minutes.", _checkInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var price = await _priceService.GetPriceAsync(_configuration.Ticker);
                    if (price.HasValue)
                    {
                        await ProcessPrice(price.Value); 
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while monitoring stock price for {Ticker}", _configuration.Ticker);
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessPrice(decimal currentPrice)
        {
            string ticker = _configuration.Ticker;
            AlertAction action = AlertAction.None;
            
            if (currentPrice > _configuration.SellingPrice) action = AlertAction.Sell;
            else if (currentPrice < _configuration.BuyingPrice) action = AlertAction.Buy;
            
            if (action != AlertAction.None)
            {
                if (IsOnCooldown(ticker, action))
                {
                    _logger.LogInformation("Alert for {Ticker} ({Action}) suppressed due to cooldown.", ticker, action);
                    return;
                }
                
                (string subject, string body) = _messageGenerator.GenerateAlertMessage(
                    ticker, currentPrice, _configuration.BuyingPrice, _configuration.SellingPrice);

                if (!string.IsNullOrEmpty(subject))
                {
                    _logger.LogWarning(">>> ALERT ACTIVATED! {Ticker} Price: {Price:N2} ({Action}) <<<", ticker, currentPrice, action);

                    if (_emailSettings.IsConfigured)
                    {
                        await _emailService.SendEmailAsync(subject, body);
                        _logger.LogInformation("Email successfully sent to {Recipient}.", _emailSettings.RecipientEmail);
                    }
                    else
                    {
                        _logger.LogWarning("Email NOT sent: Email configuration is missing or incomplete.");
                    }
                    
                    UpdateLastAlertTime(ticker, action);
                }
            }
            else
            {
                _logger.LogInformation("Maintained: {Ticker} at {Price:N2}. (Neutral range {Buy:N2} to {Sell:N2})", 
                    ticker, currentPrice, _configuration.BuyingPrice, _configuration.SellingPrice);

                ResetCooldown(ticker);
            }
        }
        
        private enum AlertAction { None, Buy, Sell }
        
        private static string GetAlertKey(string ticker, AlertAction action) => $"{ticker}_{action.ToString().ToUpperInvariant()}";
        
        private bool IsOnCooldown(string ticker, AlertAction action)
        {
            if (!_emailSettings.IsAlertCooldownEnabled) return false;
            
            string key = GetAlertKey(ticker, action);
            if (_lastAlertSent.TryGetValue(key, out DateTimeOffset lastSentTime))
            {
                TimeSpan elapsed = DateTimeOffset.UtcNow - lastSentTime;
                TimeSpan cooldownTime = TimeSpan.FromSeconds(_emailSettings.AlertCooldownSeconds);
                if (elapsed < cooldownTime) return true;
            }
            return false;
        }

        private void UpdateLastAlertTime(string ticker, AlertAction action)
        {
            string key = GetAlertKey(ticker, action);
            _lastAlertSent[key] = DateTimeOffset.UtcNow;
        }

        private void ResetCooldown(string ticker)
        {
            _lastAlertSent.Remove(GetAlertKey(ticker, AlertAction.Buy));
            _lastAlertSent.Remove(GetAlertKey(ticker, AlertAction.Sell));
        }
    }
}