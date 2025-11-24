namespace StockQuoteAlert.Tests.Domain;

public class StockAlert
{
    public static bool ShouldAlert(decimal currentPrice, decimal buyPrice, decimal sellPrice)
    {
        return currentPrice < buyPrice || currentPrice > sellPrice;
    }
}

public class StockAlertTests
{
    [Fact]
    public void ShouldAlert_WhenCurrentPriceIsBelowBuyPrice_ReturnsTrue()
    {
        decimal buyPrice = 10.00m;
        decimal sellPrice = 12.00m;
        decimal currentPrice = 9.50m;

        bool result = StockAlert.ShouldAlert(currentPrice, buyPrice, sellPrice);

        Assert.True(result); 
    }

    [Fact]
    public void ShouldAlert_WhenCurrentPriceIsAboveSellPrice_ReturnsTrue()
    {
        decimal buyPrice = 10.00m;
        decimal sellPrice = 12.00m;
        decimal currentPrice = 12.50m;

        bool result = StockAlert.ShouldAlert(currentPrice, buyPrice, sellPrice);

        Assert.True(result); 
    }

    [Fact]
    public void ShouldAlert_WhenCurrentPriceIsWithinRange_ReturnsFalse()
    {
        decimal buyPrice = 10.00m;
        decimal sellPrice = 12.00m;
        decimal currentPrice = 11.00m;

        bool result = StockAlert.ShouldAlert(currentPrice, buyPrice, sellPrice);

        Assert.False(result); 
    }
}