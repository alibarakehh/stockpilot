namespace InventoryApi.Services;

public sealed class AiProviderUnavailableException(string message) : Exception(message);

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message) : base(message) { }
    public AiProviderException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InvalidAiDraftException(string message) : Exception(message);
