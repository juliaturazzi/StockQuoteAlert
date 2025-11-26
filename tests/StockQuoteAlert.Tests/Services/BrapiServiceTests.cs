using Moq;
using System.Net;
using StockQuoteAlert.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Tests.Services;

public class BrapiServiceTests
{
    private const string SuccessJson = @"{ ""results"": [ { ""regularMarketPrice"": 45.30 } ] }";
    private const string FailureJson = @"{ ""results"": [] }";

    public class MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent) : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode = statusCode;
        private readonly string _responseContent = responseContent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
            return await Task.FromResult(response);
        }
    }
    
    private static BrapiService CreateBrapiService(HttpStatusCode statusCode, string content, string? token = "test-token-123")
    {
        var handler = new MockHttpMessageHandler(statusCode, content);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fake.brapi.dev/")
        };

        var logger = Mock.Of<ILogger<BrapiService>>();
        var settings = new MonitoringSettings 
        { 
            ApiBaseUrl = "https://fake.brapi.dev/",
            BrapiToken = token
        };

        return new BrapiService(httpClient, logger, settings);
    }

    [Fact]
    public async Task GetPriceAsync_ValidResponse_ReturnsCorrectPrice()
    {
        var service = CreateBrapiService(HttpStatusCode.OK, SuccessJson);
        var price = await service.GetPriceAsync("PETR4");

        Assert.True(price.HasValue);
        Assert.Equal(45.30m, price.Value);
    }

    [Fact]
    public async Task GetPriceAsync_EmptyResults_ReturnsNull()
    {
        var service = CreateBrapiService(HttpStatusCode.OK, FailureJson);
        var price = await service.GetPriceAsync("NOCONTENT");

        Assert.False(price.HasValue);
    }

    [Fact]
    public async Task GetPriceAsync_HttpError_ReturnsNull()
    {
        var service = CreateBrapiService(HttpStatusCode.InternalServerError, "Error");
        var price = await service.GetPriceAsync("SERVERDOWN");

        Assert.False(price.HasValue);
    }


    [Fact]
    public async Task GetPriceAsync_NoToken_FreeTicker_ReturnsPrice()
    {
        var service = CreateBrapiService(HttpStatusCode.OK, SuccessJson, token: null);
        var price = await service.GetPriceAsync("MGLU3");

        Assert.True(price.HasValue);
        Assert.Equal(45.30m, price.Value);
    }

    [Fact]
    public async Task GetPriceAsync_NoToken_RestrictedTicker_ReturnsNull()
    {
        var service = CreateBrapiService(HttpStatusCode.OK, SuccessJson, token: null);
        var price = await service.GetPriceAsync("WEGE3");

        Assert.False(price.HasValue);
    }
}