using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;
using StockQuoteAlert.Infrastructure.Services;
using StockQuoteAlert.Workers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up Stock Quote Alert...");

    if (args.Length < 3)
    {
        Log.Error("Incorrect number of arguments. Usage: StockQuoteAlert <ASSET_TICKER> <SELLING_PRICE> <BUYING_PRICE>");
        return;
    }

    var assetTicker = args[0];
    var sellingPrice = decimal.Parse(args[1]);
    var buyingPrice = decimal.Parse(args[2]);

    Log.Information("Monitoring asset {AssetTicker} for selling price {SellingPrice} and buying price {BuyingPrice}",
        assetTicker, sellingPrice, buyingPrice);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration));

    var stockConfig = new StockConfiguration(assetTicker, sellingPrice, buyingPrice);
    builder.Services.AddSingleton(stockConfig);

    builder.Services.AddSingleton<IMessageGeneratorService, EmailMessageGeneratorService>();

    var emailSettings = builder.Configuration
        .GetSection("EmailSettings")
        .Get<EmailSettings>()
        ?? throw new Exception("EmailSettings section is missing in configuration (appsettings.json).");

    builder.Services.AddSingleton(emailSettings);
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

    var monitoringSettings = builder.Configuration
        .GetSection("MonitoringSettings")
        .Get<MonitoringSettings>()
        ?? throw new Exception("MonitoringSettings section is missing in configuration (appsettings.json).");

    if (string.IsNullOrWhiteSpace(monitoringSettings.ApiBaseUrl))
    {
        throw new Exception(
            "MonitoringSettings:ApiBaseUrl is not configured. " +
            "Set MonitoringSettings__ApiBaseUrl in environment or MonitoringSettings:ApiBaseUrl in appsettings.json.");
    }

    builder.Services.AddSingleton(monitoringSettings);

    builder.Services.AddHttpClient<IStockPriceService, BrapiService>((sp, client) =>
    {
        var settings = sp.GetRequiredService<MonitoringSettings>();

        client.BaseAddress = new Uri(settings.ApiBaseUrl!);
        client.DefaultRequestHeaders.Add("User-Agent", "StockQuoteAlert-App");
    });

    builder.Services.AddHostedService<StockMonitorWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed.");
}
finally
{
    Log.CloseAndFlush();
}