using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Services;

public sealed class AiInventoryService(AppDbContext db) : IAiInventoryService
{
    public async Task<InventoryIntelligenceResponse> GenerateInsightsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var currencyCode = await db.Workspaces.AsNoTracking()
            .Where(workspace => workspace.Id == workspaceId)
            .Select(workspace => workspace.CurrencyCode)
            .SingleAsync(cancellationToken);
        var items = await db.InventoryItems.AsNoTracking()
            .Include(item => item.Category)
            .Where(item => item.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);
        var active = items
            .Where(item => item.LifecycleStatus == InventoryLifecycleStatus.Active)
            .ToList();
        var lowStock = active.Count(item => item.Status == StockStatus.LowStock);
        var outOfStock = active.Count(item => item.Status == StockStatus.OutOfStock);
        var ordered = active.Count(item => item.Status == StockStatus.Ordered);
        var riskRatio = active.Count == 0 ? 0 : (lowStock + outOfStock) / (double)active.Count;
        var healthScore = Math.Clamp((int)Math.Round(100 - riskRatio * 65 - ordered * 2.5), 0, 100);

        var insights = active
            .Select(BuildInsight)
            .Where(insight => insight is not null)
            .Cast<InventoryInsight>()
            .OrderBy(insight => insight.HealthScore)
            .ThenByDescending(insight => insight.SuggestedOrderQuantity)
            .Take(8)
            .ToList();

        var categories = items
            .GroupBy(item => item.Category.Name)
            .Select(group => new CategorySummary(
                group.Key,
                group.Count(),
                group.Sum(item => item.Quantity),
                group.Sum(item => item.Quantity * item.PurchasePrice),
                group.Count(item => item.Status is StockStatus.LowStock or StockStatus.OutOfStock or StockStatus.Ordered)))
            .OrderByDescending(category => category.Value)
            .ToList();

        var atRisk = lowStock + outOfStock;
        var summary = active.Count == 0
            ? "There are no active products to analyze. Add inventory to generate recommendations."
            : healthScore switch
            {
                >= 85 => $"Inventory is healthy. {atRisk} active item(s) need attention; prioritize the recommendations below.",
                >= 60 => $"Inventory needs attention: {atRisk} of {active.Count} active items are at or below their reorder levels.",
                _ => $"Inventory risk is high. Replenish critical products now; {atRisk} of {active.Count} active items are low or out of stock."
            };

        return new InventoryIntelligenceResponse(
            healthScore,
            items.Sum(item => item.Quantity * item.PurchasePrice),
            currencyCode,
            lowStock,
            outOfStock,
            ordered,
            items.Count(item => item.Status == StockStatus.Discontinued),
            summary,
            insights,
            categories,
            DateTime.UtcNow);
    }

    private static InventoryInsight? BuildInsight(InventoryItem item)
    {
        if (item.Status == StockStatus.Ordered)
        {
            return new InventoryInsight(
                item.Id,
                item.Name,
                item.Sku,
                "info",
                "Order in progress",
                "Confirm the expected delivery date and update stock when the shipment arrives.",
                0,
                72);
        }

        if (item.Quantity > item.ReorderLevel) return null;

        var target = Math.Max(item.ReorderLevel * 2, 1);
        var safetyStock = Math.Max((int)Math.Ceiling(item.ReorderLevel * 0.25), 1);
        var suggested = Math.Max(target + safetyStock - item.Quantity, 1);
        var stockRatio = item.ReorderLevel == 0 ? 0 : item.Quantity / (double)item.ReorderLevel;
        var score = Math.Clamp((int)Math.Round(stockRatio * 55), 0, 55);
        var critical = item.Status == StockStatus.OutOfStock || stockRatio <= 0.35;

        return new InventoryInsight(
            item.Id,
            item.Name,
            item.Sku,
            critical ? "critical" : "warning",
            critical ? "Stockout risk" : "Reorder recommended",
            $"Order approximately {suggested} units to restore two reorder cycles plus safety stock.",
            suggested,
            score);
    }
}
