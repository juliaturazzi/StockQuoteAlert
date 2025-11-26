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
        
        string assemblyLocation = typeof(HostIntegrationTests).Assembly.Location;
        string assemblyDirectory = Path.GetDirectoryName(assemblyLocation) 
            ?? throw new InvalidOperationException("Assembly directory not found.");
        
        string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "..", ".."));
        string configPath = Path.Combine(solutionRoot, "src", "StockQuoteAlert", "appsettings.json");

        builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: false);

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

        var emailSettings = builder.Configuration
            .GetSection("EmailSettings")
            .Get<EmailSettings>() 
            ?? new EmailSettings();

        builder.Services.AddSingleton(emailSettings);
        builder.Services.AddSingleton(Mock.Of<IEmailService>()); 

        var monitoringSettings = builder.Configuration
            .GetSection("MonitoringSettings")
            .Get<MonitoringSettings>()
            ?? throw new Exception("The 'MonitoringSettings' section was not found or is invalid. Please check appsettings.json.");

        if (string.IsNullOrWhiteSpace(monitoringSettings.ApiBaseUrl))
        {
             throw new Exception("MonitoringSettings:ApiBaseUrl is not configured.");
        }

        builder.Services.AddSingleton(monitoringSettings);

        builder.Services.AddHttpClient<IStockPriceService, BrapiService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<MonitoringSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl!);
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
        
        var worker = host!.Services.GetService<IHostedService>();
        Assert.NotNull(worker);
        Assert.IsType<StockMonitorWorker>(worker); 

        var priceService = host.Services.GetService<IStockPriceService>();
        var emailGenerator = host.Services.GetService<IMessageGeneratorService>();
        var emailService = host.Services.GetService<IEmailService>();
        var monSettings = host.Services.GetService<MonitoringSettings>();
        
        Assert.NotNull(priceService);
        Assert.NotNull(emailGenerator);
        Assert.NotNull(emailService);
        Assert.NotNull(monSettings);

        Assert.IsType<BrapiService>(priceService);
        Assert.False(string.IsNullOrWhiteSpace(monSettings.ApiBaseUrl)); 
    }
}