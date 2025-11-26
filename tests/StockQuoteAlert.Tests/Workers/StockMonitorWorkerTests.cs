using Moq; 
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;
using StockQuoteAlert.Workers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace StockQuoteAlert.Tests.Workers;

public class TestableStockMonitorWorker(
    ILogger<StockMonitorWorker> logger,
    IStockPriceService priceService,
    StockConfiguration configuration,
    IEmailService emailService,
    IMessageGeneratorService messageGenerator,
    EmailSettings emailSettings,
    MonitoringSettings monitoringSettings) : StockMonitorWorker(logger, priceService, configuration, emailService, messageGenerator, emailSettings, monitoringSettings)
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
        string assemblyLocation = typeof(StockMonitorWorkerTests).Assembly.Location;
        string assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? throw new InvalidOperationException("Assembly directory not found.");
        string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "..", ".."));
        string configDirectory = Path.Combine(solutionRoot, "src", "StockQuoteAlert");

        return new ConfigurationBuilder()
            .SetBasePath(configDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }

    private static EmailSettings GetEmailSettingsFromConfig()
    {
        var configuration = BuildConfiguration();
        return configuration.GetSection("EmailSettings").Get<EmailSettings>() 
               ?? throw new Exception("Failed to read EmailSettings from configuration.");
    }

    private static MonitoringSettings GetMonitoringSettingsFromConfig()
    {
        var configuration = BuildConfiguration();
        
        var settings = configuration.GetSection("MonitoringSettings").Get<MonitoringSettings>()
            ?? throw new Exception("MonitoringSettings section is missing in configuration (appsettings.json).");

        if (string.IsNullOrWhiteSpace(settings.BrapiToken))
        {
             settings.BrapiToken = "test-token-mock";
        }

        return settings;
    }
    
    [Fact]
    public async Task ExecuteAsync_PriceExceedsSellingPrice_ShouldSendSellAlertEmail()
    {
        decimal sellingPriceHit = 12.50m; 
        SetupPrice(sellingPriceHit); 
        SetupEmailService();         
        var worker = CreateWorker();
        var token = GetCancellation(); 

        await RunExecuteAsync(worker, token);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(), 
                It.IsAny<string>()
            ),
            Times.Once,
            "Should send a SELL alert email."
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

        await RunExecuteAsync(worker, token);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once,
            "Should send a BUY alert email."
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

        await RunExecuteAsync(worker, token);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Never,
            "Should NOT send any email when price is within range."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceServiceReturnsNull_ShouldNotSendEmail()
    {
        SetupPrice(null);
        SetupEmailService();
        var worker = CreateWorker();
        var token = GetCancellation(); 

        await RunExecuteAsync(worker, token);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should NOT send email if price service returns null."
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

        await RunExecuteAsync(worker, token);

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should NOT send email if price service throws exception."
        );
    }

    [Fact]
    public async Task ExecuteAsync_PriceExceedsSellingPrice_ShouldSendFirstEmailOnly()
    {
        decimal sellingPriceHit = 12.50m;
        SetupPrice(sellingPriceHit);
        SetupEmailService();
        var worker = CreateWorker();
        
        await RunExecuteAsync(worker, GetCancellation());
        await RunExecuteAsync(worker, GetCancellation());

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once,
            "Just one email should be sent during the cooldown period."
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
        
        await Task.Delay(100); 

        SetupPrice(sellingPriceHit);
        await RunExecuteAsync(worker, GetCancellation());

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2),
            "The second alert should be sent after price returns to neutral."
        );
    }

    [Fact]
    public async Task ExecuteAsync_InvalidEmailSettings_ShouldLogWarningAndNotSendEmail()
    {
        decimal sellingPriceHit = 12.50m;
        SetupPrice(sellingPriceHit);
        
        var invalidSettings = new EmailSettings(); 
        
        var logger = Mock.Of<ILogger<StockMonitorWorker>>();
        
        var worker = new TestableStockMonitorWorker(
            logger, 
            _mockPriceService.Object,
            _testConfig,
            _mockEmailService.Object,
            _mockMessageGenerator.Object,
            invalidSettings,
            _monitoringSettingsForTests
        );

        await RunExecuteAsync(worker, GetCancellation());

        _mockEmailService.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "The email should not be sent with invalid email settings."
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