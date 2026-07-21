using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace InventoryApi.Services;

public sealed partial class GeminiInventoryDraftProvider(
    HttpClient httpClient,
    IOptions<AiSmartIntakeOptions> options,
    ILogger<GeminiInventoryDraftProvider> logger)
    : IAiInventoryDraftProvider
{
    private const int MaximumResponseBytes = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiSmartIntakeOptions _options = options.Value;

    public bool IsAvailable => true;
    public string ProviderName => "Gemini";
    public string? UnavailableReason => null;

    public async Task<AiInventoryDraftCandidate> GenerateAsync(
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildRequest(description), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogProviderFailure(logger, (int)response.StatusCode);
                throw new AiProviderException("AI extraction is temporarily unavailable.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var limitedResponse = await ReadLimitedAsync(stream, cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                limitedResponse,
                cancellationToken: cancellationToken);
            var output = ExtractOutputText(document.RootElement);
            var candidate = JsonSerializer.Deserialize<AiInventoryDraftCandidate>(output, JsonOptions);
            return candidate ?? throw new InvalidAiDraftException("The AI provider returned an empty draft.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidAiDraftException)
        {
            throw;
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new InvalidAiDraftException("The AI provider returned an invalid structured draft.");
        }
        catch (InvalidOperationException)
        {
            throw new InvalidAiDraftException("The AI provider returned an invalid structured draft.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            throw new AiProviderException("AI extraction is temporarily unavailable.", exception);
        }
    }

    private object BuildRequest(string description) => new
    {
        model = _options.Model,
        messages = new object[]
        {
            new { role = "system", content = AiInventoryDraftSchema.SystemInstruction },
            new
            {
                role = "user",
                content = $"<inventory_description>\n{description}\n</inventory_description>"
            }
        },
        response_format = new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "inventory_draft",
                strict = true,
                schema = AiInventoryDraftSchema.Build()
            }
        }
    };

    private static string ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
            throw new InvalidAiDraftException("The AI provider did not return a structured draft.");

        var firstChoice = choices[0];
        if (firstChoice.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
            return content.GetString()!;

        throw new InvalidAiDraftException("The AI provider did not return a structured draft.");
    }

    private static async Task<MemoryStream> ReadLimitedAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaximumResponseBytes)
            {
                await output.DisposeAsync();
                throw new AiProviderException("AI extraction is temporarily unavailable.");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        output.Position = 0;
        return output;
    }

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Warning,
        Message = "Gemini inventory draft provider returned HTTP {StatusCode}")]
    private static partial void LogProviderFailure(ILogger logger, int statusCode);
}
