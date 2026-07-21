using Microsoft.Extensions.Options;

namespace InventoryApi.Services;

public sealed class DisabledAiInventoryDraftProvider(IOptions<AiSmartIntakeOptions> options)
    : IAiInventoryDraftProvider
{
    private readonly AiSmartIntakeOptions _options = options.Value;

    public bool IsAvailable => false;
    public string ProviderName => _options.Provider;
    public string UnavailableReason => !_options.Enabled
        ? "AI Smart Intake is not enabled. Manual item entry remains available."
        : string.IsNullOrWhiteSpace(_options.ApiKey)
            ? "AI Smart Intake requires a server-side provider key. Manual item entry remains available."
            : "The configured AI provider is not supported.";

    public Task<AiInventoryDraftCandidate> GenerateAsync(
        string description,
        CancellationToken cancellationToken) =>
        throw new AiProviderUnavailableException(UnavailableReason);
}
