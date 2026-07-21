namespace InventoryApi.Services;

public sealed class AiSmartIntakeOptions
{
    public const string SectionName = "AI:SmartIntake";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = "OpenAI";
    public string Model { get; init; } = "gpt-5.6-sol";
    public string ApiKey { get; init; } = string.Empty;
    public string Endpoint { get; init; } = "https://api.openai.com/v1/responses";
    public int TimeoutSeconds { get; init; } = 20;
}
