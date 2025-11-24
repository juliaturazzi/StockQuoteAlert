using StockQuoteAlert.Domain.Interfaces;
using System.Globalization;

namespace StockQuoteAlert.Infrastructure.Services;

public class EmailMessageGeneratorService : IMessageGeneratorService
{
    private static readonly CultureInfo UsCulture = new("en-US");
    public (string Subject, string Body) GenerateAlertMessage(
        string ticker,
        decimal currentPrice,
        decimal buyingPrice,
        decimal sellingPrice)
    {
        if (currentPrice > sellingPrice)
        {
            return GenerateSellAlert(ticker, currentPrice, sellingPrice);
        }

        if (currentPrice < buyingPrice)
        {
            return GenerateBuyAlert(ticker, currentPrice, buyingPrice);
        }

        return (string.Empty, string.Empty);
    }

    private static (string Subject, string Body) GenerateSellAlert(
        string ticker,
        decimal currentPrice,
        decimal sellingPrice)
    {
        decimal difference = currentPrice - sellingPrice;

        string subject = $"{ticker} hit your sell target at {currentPrice.ToString("C2", UsCulture)}";

        string body = BuildAlertBody(
            alertType: "Sell",
            ticker: ticker,
            currentPrice: currentPrice,
            targetPrice: sellingPrice,
            difference: difference,
            directionText: "above",
            actionLine: "Consider locking in profits or rebalancing your position.",
            accentColorHex: "#dc2626"
        );

        return (subject, body);
    }

    private static (string Subject, string Body) GenerateBuyAlert(
        string ticker,
        decimal currentPrice,
        decimal buyingPrice)
    {
        decimal difference = buyingPrice - currentPrice;

        string subject = $"{ticker} entered your buy zone at {currentPrice.ToString("C2", UsCulture)}";

        string body = BuildAlertBody(
            alertType: "Buy",
            ticker: ticker,
            currentPrice: currentPrice,
            targetPrice: buyingPrice,
            difference: difference,
            directionText: "below",
            actionLine: "Consider opening or adding to your position.",
            accentColorHex: "#16a34a"
        );

        return (subject, body);
    }

    private static string BuildAlertBody(
        string alertType,
        string ticker,
        decimal currentPrice,
        decimal targetPrice,
        decimal difference,
        string directionText,
        string actionLine,
        string accentColorHex)
    {
        string alertTypeUpper = alertType.ToUpperInvariant();
        string alertTypeLower = alertType.ToLowerInvariant();

        string currentPriceFormatted = currentPrice.ToString("C2", UsCulture);
        string targetPriceFormatted = targetPrice.ToString("C2", UsCulture);
        string differenceFormatted = difference.ToString("C2", UsCulture);

        string body = $@"
        <html>
        <head>
            <meta charset=""UTF-8"" />
            <title>{alertType} alert for {ticker}</title>
        </head>
        <body style=""margin:0;padding:0;"">
            <!-- Preheader -->
            <div style=""display:none;max-height:0;overflow:hidden;font-size:1px;line-height:1px;color:#ffffff;opacity:0;"">
            Price alert for {ticker}: now at {currentPriceFormatted}, {directionText} your {alertTypeLower} target of {targetPriceFormatted}.
            </div>

            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
            <tr>
                <td align=""center"" style=""padding:24px 16px;"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" 
                        style=""max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;
                                font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
                    
                    <!-- Header -->
                    <tr>
                    <td style=""padding:20px 24px 16px 24px;background-color:#0b1f33;color:#ffffff;"">
                        <div style=""font-size:13px;letter-spacing:0.16em;text-transform:uppercase;opacity:0.8;"">
                        Stock Quote Alert
                        </div>
                        <div style=""font-size:24px;font-weight:700;margin-top:8px;"">
                        {alertTypeUpper} signal for {ticker}
                        </div>
                    </td>
                    </tr>

                    <!-- Intro -->
                    <tr>
                    <td style=""padding:20px 24px 8px 24px;"">
                        <p style=""margin:0 0 12px 0;font-size:14px;color:#64748b;"">
                        We're keeping an eye on <strong>{ticker}</strong> for you.
                        </p>
                        <p style=""margin:0 0 12px 0;font-size:15px;color:#0f172a;"">
                        The price has just moved {directionText} your {alertTypeLower} threshold and may need your review.
                        </p>
                        <p style=""margin:0 0 16px 0;font-size:14px;color:#0f172a;"">
                        {actionLine}
                        </p>
                    </td>
                    </tr>

                    <!-- Info card -->
                    <tr>
                    <td style=""padding:0 24px 16px 24px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" 
                            style=""border-collapse:collapse;border-radius:8px;background-color:#f8fafc;"">
                        <tr>
                            <td style=""padding:12px 16px;font-size:13px;color:#0f172a;"">
                            <div style=""margin-bottom:6px;""><strong>Ticker:</strong> {ticker}</div>
                            <div style=""margin-bottom:6px;""><strong>Current price:</strong> {currentPriceFormatted}</div>
                            <div style=""margin-bottom:6px;""><strong>{alertType} target:</strong> {targetPriceFormatted}</div>
                            <div style=""margin-bottom:0;""><strong>Difference:</strong> {differenceFormatted} {directionText} target</div>
                            </td>
                        </tr>
                        </table>
                    </td>
                    </tr>
                </table>
                </td>
            </tr>
            </table>
        </body>
        </html>";

        return body;
    }
}
