using InventoryApi.Models;

namespace InventoryApi.Contracts;

public sealed record InventoryInsight(
    Guid ItemId,
    string ItemName,
    string Sku,
    string Severity,
    string Title,
    string Recommendation,
    int SuggestedOrderQuantity,
    int HealthScore);

public sealed record InventoryIntelligenceResponse(
    int OverallHealthScore,
    decimal TotalInventoryValue,
    string CurrencyCode,
    int LowStockCount,
    int OutOfStockCount,
    int OrderedCount,
    int DiscontinuedCount,
    string ExecutiveSummary,
    IReadOnlyList<InventoryInsight> Insights,
    IReadOnlyList<CategorySummary> Categories,
    DateTime GeneratedAtUtc);

public sealed record CategorySummary(
    string Category,
    int ItemCount,
    int Units,
    decimal Value,
    int AtRiskCount);
