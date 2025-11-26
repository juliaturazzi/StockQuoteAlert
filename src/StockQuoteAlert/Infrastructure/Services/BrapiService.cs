using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StockQuoteAlert.Domain.DTOs; 
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Infrastructure.Services
{
    public class BrapiService(HttpClient httpClient, ILogger<BrapiService> logger, MonitoringSettings settings) : IStockPriceService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<BrapiService> _logger = logger;
        private readonly MonitoringSettings _settings = settings;

        private static readonly HashSet<string> _freeTickers = new(StringComparer.OrdinalIgnoreCase)
        {
            "PETR4", "MGLU3", "VALE3", "ITUB4"
        };

        public async Task<decimal?> GetPriceAsync(string symbol)
        {
            try
            {
                bool hasToken = !string.IsNullOrWhiteSpace(_settings.BrapiToken);

                if (!hasToken)
                {
                    if (!_freeTickers.Contains(symbol))
                    {
                        _logger.LogError("Access Denied: The ticker {Symbol} requires an authentication token (BRAPI_TOKEN). Only {FreeTickers} are free without token.", 
                            symbol, string.Join(", ", _freeTickers));
                        return null; 
                    }
                }

                var requestUrl = $"api/quote/{symbol}?range=1d&interval=1d";
                
                if (hasToken)
                {
                    requestUrl += $"&token={_settings.BrapiToken}";
                }

                var response = await _httpClient.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("AUTH ERROR (401): The Brapi Token provided is invalid or expired. Please check your BRAPI_TOKEN in the .env file.");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Ticker {Symbol} not found in Brapi API (404).", symbol);
                    }
                    else
                    {
                        _logger.LogError("Brapi API request failed for {Symbol}. Status Code: {StatusCode}", symbol, response.StatusCode);
                    }
                    
                    return null;
                }

                var data = await response.Content.ReadFromJsonAsync<BrapiResponse>();
                var price = data?.Results?.FirstOrDefault()?.RegularMarketPrice;

                if (price is null)
                {
                    _logger.LogWarning("The API returned success (200), but no price data was found for {Symbol}", symbol);
                    return null;
                }

                _logger.LogInformation("Current price for {Symbol}: R$ {Price}", symbol, price);
                return price;
            }
            catch (Exception ex)
            {
                _logger.LogError("Network/Unexpected error while calling Brapi API for {Symbol}: {Message}", symbol, ex.Message);
                return null;
            }
        }
    }
}