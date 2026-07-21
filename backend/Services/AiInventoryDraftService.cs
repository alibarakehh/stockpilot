using InventoryApi.Contracts;

namespace InventoryApi.Services;

public sealed class AiInventoryDraftService(IAiInventoryDraftProvider provider)
    : IAiInventoryDraftService
{
    public AiSmartIntakeAvailabilityResponse GetAvailability() => new(
        provider.IsAvailable,
        provider.ProviderName,
        provider.UnavailableReason);

    public async Task<AiInventoryDraftResponse> GenerateAsync(
        string description,
        CancellationToken cancellationToken)
    {
        if (!provider.IsAvailable)
            throw new AiProviderUnavailableException(
                provider.UnavailableReason ?? "AI Smart Intake is unavailable.");

        var candidate = await provider.GenerateAsync(description.Trim(), cancellationToken);
        Validate(candidate);

        var fields = new List<string>
        {
            "name", "description", "category", "quantity", "reorderLevel",
            "purchasePrice", "sellingPrice"
        };
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(candidate.Sku)) fields.Insert(1, "sku");
        else warnings.Add("No reliable SKU was found. Add a unique SKU before saving.");
        if (!string.IsNullOrWhiteSpace(candidate.Supplier)) fields.Add("supplier");
        if (!string.IsNullOrWhiteSpace(candidate.Location)) fields.Add("location");

        return new AiInventoryDraftResponse(
            candidate.Name.Trim(),
            candidate.Sku?.Trim() ?? string.Empty,
            candidate.Description.Trim(),
            candidate.Category.Trim(),
            candidate.Quantity,
            candidate.ReorderLevel,
            candidate.PurchasePrice,
            candidate.SellingPrice,
            candidate.Supplier?.Trim() ?? string.Empty,
            candidate.Location?.Trim() ?? string.Empty,
            fields,
            warnings);
    }

    private static void Validate(AiInventoryDraftCandidate candidate)
    {
        Require(candidate.Name, "name", 160);
        Optional(candidate.Sku, "SKU", 80);
        Require(candidate.Description, "description", 1000);
        Require(candidate.Category, "category", 100);
        Optional(candidate.Supplier, "supplier", 160);
        Optional(candidate.Location, "location", 120);

        if (candidate.Quantity < 0)
            throw new InvalidAiDraftException("The AI draft contained a negative quantity.");
        if (candidate.ReorderLevel < 0)
            throw new InvalidAiDraftException("The AI draft contained a negative reorder level.");
        if (candidate.PurchasePrice is < 0 or > 999999999)
            throw new InvalidAiDraftException("The AI draft contained an invalid purchase price.");
        if (candidate.SellingPrice is < 0 or > 999999999)
            throw new InvalidAiDraftException("The AI draft contained an invalid selling price.");
    }

    private static void Require(string value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidAiDraftException($"The AI draft did not provide a {field}.");
        Optional(value, field, maximumLength);
    }

    private static void Optional(string? value, string field, int maximumLength)
    {
        if ((value ?? string.Empty).Trim().Length > maximumLength)
            throw new InvalidAiDraftException($"The AI draft {field} exceeded {maximumLength} characters.");
    }
}
