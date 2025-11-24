using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StockQuoteAlert.Domain.DTOs;
using StockQuoteAlert.Domain.Interfaces;

namespace StockQuoteAlert.Infrastructure.Services
{
    public class BrapiService(HttpClient httpClient, ILogger<BrapiService> logger) : IStockPriceService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<BrapiService> _logger = logger;

        public async Task<decimal?> GetPriceAsync(string symbol)
        {
        
            try
            {
                var response = await _httpClient.GetFromJsonAsync<BrapiResponse>($"api/quote/{symbol}?range=1d&interval=1d");
                var price = response?.Results?.FirstOrDefault()?.RegularMarketPrice;

                if (price is null)
                {
                    _logger.LogWarning("The API returned a response, but no price was found for {Symbol}", symbol);
                    return null;
                }

                _logger.LogInformation("Current price for {Symbol}: R$ {Price}", symbol, price);
                return price;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling the Brapi API for {Symbol}", symbol);
                return null;
            }
        }
    }
}