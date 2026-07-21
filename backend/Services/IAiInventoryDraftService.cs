using InventoryApi.Contracts;

namespace InventoryApi.Services;

public interface IAiInventoryDraftService
{
    AiSmartIntakeAvailabilityResponse GetAvailability();

    Task<AiInventoryDraftResponse> GenerateAsync(
        string description,
        CancellationToken cancellationToken);
}
