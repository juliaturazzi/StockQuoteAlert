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
        IEmailService emailService) : BackgroundService
    {
        private readonly ILogger<StockMonitorWorker> _logger = logger;
        private readonly IStockPriceService _priceService = priceService;
        private readonly StockConfiguration _configuration = configuration;
    
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly IEmailService _emailService = emailService;

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
                        ProcessPrice(price.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while monitoring stock price for {Ticker}", _configuration.Ticker);
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async void ProcessPrice(decimal currentPrice)
        {
            if (currentPrice > _configuration.SellingPrice)
            {
                _logger.LogWarning("SELL ALERT! The price of {Ticker} is {Price}, exceeding the target of {Target}", 
                    _configuration.Ticker, currentPrice, _configuration.SellingPrice);
            
                await _emailService.SendEmailAsync(
                    subject: $"SELL {_configuration.Ticker} NOW!", 
                    body: $"The price has risen to R$ {currentPrice}. The target was to sell above R$ {_configuration.SellingPrice}.");

            }
            else if (currentPrice < _configuration.BuyingPrice)
            {
                _logger.LogWarning("BUY ALERT! The price of {Ticker} is {Price}, below the target of {Target}", 
                    _configuration.Ticker, currentPrice, _configuration.BuyingPrice);
            
                await _emailService.SendEmailAsync(
                    subject: $"BUY {_configuration.Ticker} NOW!", 
                    body: $"The price has dropped to R$ {currentPrice}. The target was to buy below R$ {_configuration.BuyingPrice}.");
            }
            else
            {
                _logger.LogInformation("Maintained: {Ticker} at R$ {Price}. (Neutral range)", _configuration.Ticker, currentPrice);
            }
        }
    }
}