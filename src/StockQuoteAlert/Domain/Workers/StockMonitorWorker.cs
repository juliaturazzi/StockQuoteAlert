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
        IMessageGeneratorService messageGenerator) : BackgroundService
    {
        private readonly ILogger<StockMonitorWorker> _logger = logger;
        private readonly IStockPriceService _priceService = priceService;
        private readonly StockConfiguration _configuration = configuration;
        private readonly IEmailService _emailService = emailService;
        private readonly IMessageGeneratorService _messageGenerator = messageGenerator;
    
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); 

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting stock monitor for {Ticker} with buying price {BuyingPrice} and selling price {SellingPrice}",
                _configuration.Ticker, _configuration.BuyingPrice, _configuration.SellingPrice);

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
            
            if (currentPrice > _configuration.SellingPrice || currentPrice < _configuration.BuyingPrice)
            {
                (string subject, string body) = _messageGenerator.GenerateAlertMessage(
                    ticker, 
                    currentPrice, 
                    _configuration.BuyingPrice, 
                    _configuration.SellingPrice
                );

                if (!string.IsNullOrEmpty(subject))
                {
                    _logger.LogWarning("ALERT ACTIVATED! {Ticker} Price: {Price}", ticker, currentPrice);
                    await _emailService.SendEmailAsync(subject, body);
                }
            }
            
            else
            {
                _logger.LogInformation("Maintained: {Ticker} at {Price}. (Neutral range {Buy} to {Sell})", 
                    ticker, currentPrice, _configuration.BuyingPrice, _configuration.SellingPrice);
            }
        }
    }
}