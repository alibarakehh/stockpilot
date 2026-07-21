using InventoryApi.Contracts;

namespace InventoryApi.Services;

public interface IAiInventoryService
{
    Task<InventoryIntelligenceResponse> GenerateInsightsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
