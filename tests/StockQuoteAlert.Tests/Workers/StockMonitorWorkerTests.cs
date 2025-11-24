using Moq;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;
using StockQuoteAlert.Workers;
using Microsoft.Extensions.Logging;

namespace StockQuoteAlert.Tests.Workers;

public class TestableStockMonitorWorker : StockMonitorWorker
{
    public TestableStockMonitorWorker(
        ILogger<StockMonitorWorker> logger,
        IStockPriceService priceService,
        StockConfiguration configuration,
        IEmailService emailService,
        IMessageGeneratorService messageGenerator)
        : base(logger, priceService, configuration, emailService, messageGenerator)
    {
    }

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
            _mockMessageGenerator.Object
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

    private CancellationToken GetCancellation(int delayMs = 500) 
    {
        var cts = new CancellationTokenSource(delayMs);
        return cts.Token;
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
            "The message generator should NOT be called when no alert is needed."
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
            "Should NOT call the message generator if the price is null."
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
            "Should NOT call the message generator if there is an exception."
        );
    }
}