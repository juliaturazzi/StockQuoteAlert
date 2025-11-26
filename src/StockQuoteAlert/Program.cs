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

    if (args.Length < 3 || args.Any(string.IsNullOrWhiteSpace))
    {
        Log.Fatal("===============================================================");
        Log.Fatal("CRITICAL ERROR: Mandatory configuration missing!");
        Log.Fatal("You must configure the following variables in your .env file:");
        Log.Fatal(" - TICKER_TO_MONITOR (e.g., PETR4)");
        Log.Fatal(" - PRICE_SELL_TARGET (e.g., 40.50)");
        Log.Fatal(" - PRICE_BUY_TARGET  (e.g., 30.00)");
        Log.Fatal("===============================================================");
        return;
    }

    var assetTicker = args[0];
    
    if (!decimal.TryParse(args[1], out var sellingPrice) || !decimal.TryParse(args[2], out var buyingPrice))
    {
        Log.Fatal("ERROR: Sell and Buy prices must be valid numbers (e.g., 22.50).");
        return;
    }

    Log.Information("Monitoring asset {AssetTicker} for selling price {SellingPrice} and buying price {BuyingPrice}",
        assetTicker, sellingPrice, buyingPrice);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration));

    var stockConfig = new StockConfiguration(assetTicker, sellingPrice, buyingPrice);
    builder.Services.AddSingleton(stockConfig);

    builder.Services.AddSingleton<IMessageGeneratorService, EmailMessageGeneratorService>();

    var emailSettings = builder.Configuration.GetSection("EmailSettings").Get<EmailSettings>() 
                        ?? new EmailSettings();

    if (!emailSettings.IsConfigured)
    {
        Log.Warning("-----------------------------------------------------------------------");
        Log.Warning("WARNING: Email configuration is incomplete.");
        Log.Warning("Monitoring will continue, but alerts will be visual (LOGS) only.");
        Log.Warning("To enable emails, please fill in EMAIL_SMTP_USER, EMAIL_SMTP_PASS,");
        Log.Warning("EMAIL_SMTP_SERVER, EMAIL_SMTP_PORT, EMAIL_SMTP_SENDER, and");
        Log.Warning("EMAIL_SMTP_RECIPIENT in your .env file.");
        Log.Warning("-----------------------------------------------------------------------");
    }
    else
    {
        Log.Information("Email settings configured correctly. Alerts will be sent to {Recipient}.", emailSettings.RecipientEmail);
    }

    builder.Services.AddSingleton(emailSettings);
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

    var monitoringSettings = builder.Configuration
        .GetSection("MonitoringSettings")
        .Get<MonitoringSettings>()
        ?? throw new Exception("MonitoringSettings section is missing in configuration (appsettings.json).");

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

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}