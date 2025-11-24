using System.Text.Json.Serialization;

namespace StockQuoteAlert.Domain.DTOs;

public record BrapiResponse(
    [property: JsonPropertyName("results")] List<StockResult> Results
);

public record StockResult(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("regularMarketPrice")] decimal RegularMarketPrice
);