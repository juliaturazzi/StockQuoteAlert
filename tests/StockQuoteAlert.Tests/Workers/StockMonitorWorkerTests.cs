using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;
using StockQuoteAlert.Workers;

namespace StockQuoteAlert.Tests.Workers;

public class TestableStockMonitorWorker(
    ILogger<StockMonitorWorker> logger,
    IStockPriceService priceService,
    StockConfiguration configuration,
    IEmailService emailService,
    IMessageGeneratorService messageGenerator,
    EmailSettings emailSettings,
    MonitoringSettings monitoringSettings)
    : StockMonitorWorker(logger, priceService, configuration, emailService, messageGenerator, emailSettings, monitoringSettings)
{
    public new Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return base.ExecuteAsync(stoppingToken);
    }
}

public class StockMonitorWorkerTests
{
    private readonly Mock<IStockPriceService> _mockPriceService = new();
    private readonly Mock<IEmailService> _mockEmailService = new();
    private readonly Mock<IMessageGeneratorService> _mockMessageGenerator = new();

    private readonly EmailSettings _emailSettingsForTests = GetEmailSettingsFromConfig();
    private readonly MonitoringSettings _monitoringSettingsForTests = GetMonitoringSettingsFromConfig();

    private readonly StockConfiguration _testConfig = new("APPL34", 12.00m, 10.00m);

    private TestableStockMonitorWorker CreateWorker()
    {
        var logger = Mock.Of<ILogger<StockMonitorWorker>>();

        _mockMessageGenerator
            .Setup(g => g.GenerateAlertMessage(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()))
            .Returns(("TEST SUBJECT", "TEST BODY"));

        return new TestableStockMonitorWorker(
            logger,
            _mockPriceService.Object,
            _testConfig,
            _mockEmailService.Object,
            _mockMessageGenerator.Object,
            _emailSettingsForTests,
            _monitoringSettingsForTests
        );
    }

    private void SetupPrice(decimal? price)
    {
        _mockPriceService
            .Setup(s => s.GetPriceAsync(_testConfig.Ticker))
            .ReturnsAsync(price);
    }

    private void SetupEmailService()
    {
        _mockEmailService
            .Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private static CancellationToken GetCancellation(int delayMs = 500)
    {
        var cts = new CancellationTokenSource(delayMs);
        return cts.Token;
    }


    private static IConfigurationRoot BuildConfiguration()
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = Path.GetFullPath(Path.Combine(
            baseDir,
            "..", "..", "..", "..", ".."
        ));

        string configDirectory = Path.Combine(repoRoot, "src", "StockQuoteAlert");

        if (!Directory.Exists(configDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Project directory not found: {configDirectory}");
        }

        return new ConfigurationBuilder()
            .SetBasePath(configDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }

    private static EmailSettings GetEmailSettingsFromConfig()
    {
        var configuration = BuildConfiguration();

        return configuration.GetSection("EmailSettings").Get<EmailSettings>()
               ?? throw new Exception("Failed to read EmailSettings from appsettings.json. Check the path and structure.");
    }

    private static MonitoringSettings GetMonitoringSettingsFromConfig()
    {
        var configuration = BuildConfiguration();

        var settings = configuration.GetSection("MonitoringSettings").Get<MonitoringSettings>()
                       ?? throw new Exception("Failed to read MonitoringSettings from appsettings.json. Check the path and structure.");

        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        {
            throw new Exception(
                "MonitoringSettings:ApiBaseUrl is not configured. " +
                "Set MonitoringSettings__ApiBaseUrl in the environment or MonitoringSettings:ApiBaseUrl in appsettings.json.");
        }

        if (settings.CheckIntervalMinutes <= 0)
        {
            throw new Exception("MonitoringSettings:CheckIntervalMinutes must be greater than zero.");
        }

        return settings;
    }

    [Fact]
    public async Task ExecuteAsync_PriceExceedsSellingPrice_ShouldSendFirstEmailOnly()
    {
        decimal sellingPriceHit = 12.50m;
        SetupPrice(sellingPriceHit);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        await RunExecuteAsync(worker, token);

        var token2 = GetCancellation();
        await RunExecuteAsync(worker, token2);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once,
            "Only the first email should be sent due to cooldown."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceIsWithinRangeThenAlerts_ShouldResetCooldown()
    {
        decimal sellingPriceHit = 12.50m;
        decimal neutralPrice = 11.00m;
        SetupEmailService();
        var worker = CreateWorker();

        SetupPrice(sellingPriceHit);
        await RunExecuteAsync(worker, GetCancellation());

        SetupPrice(neutralPrice);
        await RunExecuteAsync(worker, GetCancellation());

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        SetupPrice(sellingPriceHit);
        await RunExecuteAsync(worker, GetCancellation());

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Exactly(2),
            "The alert should be sent twice because the price returned to the neutral range in between."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceExceedsSellingPrice_ShouldSendSellAlertEmail()
    {
        decimal sellingPriceHit = 12.50m;
        SetupPrice(sellingPriceHit);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        try 
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once,
            "Should send a SELL alert email."
        );
        
        _mockMessageGenerator.Verify(
            g => g.GenerateAlertMessage(
                It.IsAny<string>(), 
                It.IsAny<decimal>(), 
                It.IsAny<decimal>(), 
                It.IsAny<decimal>()
            ),
            Times.Once,
            "The message generator should be called to create the email body."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceDropsBelowBuyingPrice_ShouldSendBuyAlertEmail()
    {
        decimal buyingPriceHit = 9.50m;
        SetupPrice(buyingPriceHit);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        try 
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once,
            "Should send a BUY alert email."
        );
        
        _mockMessageGenerator.Verify(
            g => g.GenerateAlertMessage(
                It.IsAny<string>(), 
                It.IsAny<decimal>(), 
                It.IsAny<decimal>(), 
                It.IsAny<decimal>()
            ),
            Times.Once,
            "The message generator should be called to create the email body."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceIsWithinRange_ShouldNotSendEmail()
    {
        decimal neutralPrice = 11.00m;
        SetupPrice(neutralPrice);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        try 
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never,
            "Should NOT send an email when price is within the buying and selling range."
        );

        _mockMessageGenerator.Verify(
            g => g.GenerateAlertMessage(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()
            ),
            Times.Never,
            "Message generator should NOT be called when no alert is needed."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceServiceReturnsNull_ShouldNotSendEmail()
    {
        SetupPrice(null);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        try 
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should NOT send an email if the price is null."
        );

        _mockMessageGenerator.Verify(
            g => g.GenerateAlertMessage(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()
            ),
            Times.Never,
            "Message generator should NOT be called if the price is null."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceServiceThrowsException_ShouldNotSendEmail()
    {
        _mockPriceService
            .Setup(s => s.GetPriceAsync(_testConfig.Ticker))
            .ThrowsAsync(new System.Net.WebException("API Down"));

        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation();

        try 
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should NOT send an email if there is an exception."
        );

        _mockMessageGenerator.Verify(
            g => g.GenerateAlertMessage(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()
            ),
            Times.Never,
            "Message generator should NOT be called if there is an exception."
        );
    }

    private static async Task RunExecuteAsync(TestableStockMonitorWorker worker, CancellationToken token)
    {
        try
        {
            await worker.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}