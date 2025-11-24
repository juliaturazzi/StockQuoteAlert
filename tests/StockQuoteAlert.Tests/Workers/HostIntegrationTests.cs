using Moq; 
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;
using StockQuoteAlert.Infrastructure.Services;
using StockQuoteAlert.Workers;

using Microsoft.Extensions.Configuration;

namespace StockQuoteAlert.Tests.Workers;

public static class TestHost
{
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        
        if (args.Length < 3)
        {
            throw new ArgumentException("Command line arguments (Ticker, Sell, Buy) are required for the Host test.");
        }
        var assetTicker = args[0];
        var sellingPrice = decimal.Parse(args[1]);
        var buyingPrice = decimal.Parse(args[2]);

        var stockConfig = new StockConfiguration(assetTicker, sellingPrice, buyingPrice);
        builder.Services.AddSingleton(stockConfig);
        
        builder.Services.AddSingleton<IMessageGeneratorService, EmailMessageGeneratorService>();

        var emailSettings = builder.Configuration.GetSection("EmailSettings").Get<EmailSettings>() 
            ?? throw new Exception("The 'EmailSettings' section was not found or is invalid. Please check appsettings.json.");

        builder.Services.AddSingleton(emailSettings);
        builder.Services.AddSingleton(Mock.Of<IEmailService>()); 
        
        builder.Services.AddHttpClient<IStockPriceService, BrapiService>(client =>
        {
            client.BaseAddress = new Uri("https://brapi.dev/"); 
            client.DefaultRequestHeaders.Add("User-Agent", "StockQuoteAlert-App");
        });

        builder.Services.AddHostedService<StockMonitorWorker>();
        
        return builder.Build();
    }
}

public class HostIntegrationTests
{
    [Fact]
    public void Application_Host_ShouldInitializeSuccessfullyAndResolveAllDependencies()
    {
        string[] testArgs = ["TEST", "10.00", "5.00"];

        IHost? host = null;
        Exception? capturedException = null;

        try
        {
            host = TestHost.CreateHost(testArgs);
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        Assert.Null(capturedException);
        Assert.NotNull(host);
        
        var worker = host.Services.GetService<IHostedService>();
        
        Assert.NotNull(worker);
        Assert.IsType<StockMonitorWorker>(worker); 

        var priceService = host.Services.GetService<IStockPriceService>();
        var emailGenerator = host.Services.GetService<IMessageGeneratorService>();
        var emailService = host.Services.GetService<IEmailService>();
        
        Assert.NotNull(priceService);
        Assert.NotNull(emailGenerator);
        Assert.NotNull(emailService);

        Assert.IsType<BrapiService>(priceService); 
    }
}