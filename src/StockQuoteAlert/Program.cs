using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockQuoteAlert.Domain.Entities;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Infrastructure.Services;

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

    builder.Services.Configure<NotificationSettings>(
        builder.Configuration.GetSection("NotificationSettings"));

    builder.Services.AddHttpClient<IStockPriceService, BrapiService>(client =>
    {
        client.BaseAddress = new Uri("https://brapi.dev/");
        client.DefaultRequestHeaders.Add("User-Agent", "StockQuoteAlert-App");
    });

    // builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
    // builder.Services.AddHttpClient<IStockPriceService, HgFinanceService>();

    // builder.Services.AddHostedService<StockMonitorWorker>();

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}