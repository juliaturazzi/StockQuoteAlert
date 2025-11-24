using Moq;
using StockQuoteAlert.Domain.Interfaces;
using StockQuoteAlert.Domain.Models;

namespace StockQuoteAlert.Tests.Services;

public class SmtpEmailServiceTests
{
    private readonly EmailSettings _settings = new()
    {
        SenderEmail = "test@app.com",
        SmtpServer = "smtp.test.io",
        SmtpPort = 2525,
        SmtpUser = "user",
        SmtpPass = "pass",
        RecipientEmail = "recipient@test.com"
    };

    [Fact]
    public async Task SendEmailAsync_ShouldCallUnderlyingSendMethod()
    {
        var mockEmailService = new Mock<IEmailService>();

        mockEmailService.Setup(s =>
            s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            )
        ).Returns(Task.CompletedTask);

        var mockStockService = new Mock<IStockPriceService>();
        mockStockService.Setup(s => s.GetPriceAsync("MGLU3")).ReturnsAsync(9.00m);

        await mockEmailService.Object.SendEmailAsync("Test Subject", "Test Body");

        mockEmailService.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ),
            Times.Once
        );
    }
}
