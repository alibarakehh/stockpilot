using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InventoryApi.Contracts;
using InventoryApi.Infrastructure;
using InventoryApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InventoryApi.Tests;

public sealed class AiSmartIntakeTests : IClassFixture<StockPilotFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AiInventoryDraftCandidate ValidCandidate = new(
        "MX Master 3S Wireless Mouse",
        "LOGI-MX3S",
        "Logitech MX Master wireless mouse.",
        "Electronics",
        25,
        5,
        72m,
        99m,
        "",
        "Shelf A3");

    private readonly StockPilotFactory _factory;

    public AiSmartIntakeTests(StockPilotFactory factory) => _factory = factory;

    [Fact]
    public async Task Valid_draft_is_reviewable_and_does_not_create_inventory()
    {
        using var factory = WithProvider(new FakeDraftProvider(ValidCandidate));
        using var client = factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        var before = await InventoryTotalAsync(client);

        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/ai/inventory-draft",
            new AiInventoryDraftRequest
            {
                Description = "Add 25 Logitech mice at $72, sell $99, Shelf A3, reorder at five."
            },
            csrf);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var draft = await response.Content.ReadFromJsonAsync<AiInventoryDraftResponse>();
        Assert.NotNull(draft);
        Assert.Equal("MX Master 3S Wireless Mouse", draft.Name);
        Assert.Contains("name", draft.GeneratedFields);
        Assert.Equal(before, await InventoryTotalAsync(client));
    }

    [Fact]
    public async Task Viewer_cannot_invoke_write_oriented_AI()
    {
        using var factory = WithProvider(new FakeDraftProvider(ValidCandidate));
        using var client = factory.CreateClient();
        var csrf = await LoginAsync(client, "viewer@test.local");
        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/ai/inventory-draft",
            new AiInventoryDraftRequest { Description = "Create a valid inventory draft please." },
            csrf);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ApiErrorCodes.PermissionDenied);
    }

    [Fact]
    public async Task Missing_provider_configuration_fails_safely()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        var availability = await client.GetFromJsonAsync<AiSmartIntakeAvailabilityResponse>(
            "/api/ai/inventory-draft/availability");
        Assert.NotNull(availability);
        Assert.False(availability.Available);

        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/ai/inventory-draft",
            new AiInventoryDraftRequest { Description = "Create a valid inventory draft please." },
            csrf);
        var response = await client.SendAsync(request);

        await AssertProblemAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            ApiErrorCodes.AiProviderUnavailable);
    }

    [Fact]
    public async Task Invalid_provider_output_is_rejected()
    {
        var invalid = ValidCandidate with { Quantity = -1 };
        using var factory = WithProvider(new FakeDraftProvider(invalid));
        using var client = factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/ai/inventory-draft",
            new AiInventoryDraftRequest { Description = "Create a valid inventory draft please." },
            csrf);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ApiErrorCodes.AiInvalidOutput);
    }

    [Fact]
    public async Task Provider_failure_returns_safe_error_and_preserves_inventory()
    {
        using var factory = WithProvider(new FailingDraftProvider());
        using var client = factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        var before = await InventoryTotalAsync(client);
        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/ai/inventory-draft",
            new AiInventoryDraftRequest { Description = "Create a valid inventory draft please." },
            csrf);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            ApiErrorCodes.AiProviderUnavailable);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("provider-internal-secret", problem, StringComparison.Ordinal);
        Assert.Equal(before, await InventoryTotalAsync(client));
    }

    [Fact]
    public async Task OpenAI_adapter_requests_strict_schema_and_parses_output()
    {
        var output = JsonSerializer.Serialize(ValidCandidate, JsonOptions);
        var responseBody = JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new { content = new[] { new { type = "output_text", text = output } } }
            }
        });
        var handler = new RecordingHandler(responseBody);
        var options = Options.Create(new AiSmartIntakeOptions
        {
            Enabled = true,
            ApiKey = "test-only-key",
            Model = "test-model",
            Endpoint = "https://example.test/v1/responses"
        });
        var provider = new OpenAiInventoryDraftProvider(
            new HttpClient(handler),
            options,
            NullLogger<OpenAiInventoryDraftProvider>.Instance);

        var candidate = await provider.GenerateAsync("Untrusted description", CancellationToken.None);

        Assert.Equal(ValidCandidate, candidate);
        Assert.NotNull(handler.RequestBody);
        using var request = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("test-model", request.RootElement.GetProperty("model").GetString());
        var format = request.RootElement.GetProperty("text").GetProperty("format");
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.False(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task OpenAI_adapter_rejects_malformed_structured_output()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new { content = new[] { new { type = "output_text", text = "{not-json" } } }
            }
        });
        var provider = new OpenAiInventoryDraftProvider(
            new HttpClient(new RecordingHandler(responseBody)),
            Options.Create(new AiSmartIntakeOptions
            {
                Enabled = true,
                ApiKey = "test-only-key",
                Endpoint = "https://example.test/v1/responses"
            }),
            NullLogger<OpenAiInventoryDraftProvider>.Instance);

        await Assert.ThrowsAsync<InvalidAiDraftException>(() =>
            provider.GenerateAsync("Untrusted description", CancellationToken.None));
    }

    [Fact]
    public async Task Gemini_adapter_requests_strict_schema_and_parses_output()
    {
        var output = JsonSerializer.Serialize(ValidCandidate, JsonOptions);
        var responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = output } }
            }
        });
        var handler = new RecordingHandler(responseBody);
        var options = Options.Create(new AiSmartIntakeOptions
        {
            Enabled = true,
            Provider = "Gemini",
            ApiKey = "test-only-key",
            Model = "test-gemini-model",
            Endpoint = "https://example.test/openai/chat/completions"
        });
        var provider = new GeminiInventoryDraftProvider(
            new HttpClient(handler),
            options,
            NullLogger<GeminiInventoryDraftProvider>.Instance);

        var candidate = await provider.GenerateAsync("Untrusted description", CancellationToken.None);

        Assert.Equal(ValidCandidate, candidate);
        Assert.NotNull(handler.RequestBody);
        using var request = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("test-gemini-model", request.RootElement.GetProperty("model").GetString());
        var format = request.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var jsonSchema = format.GetProperty("json_schema");
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.False(jsonSchema.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
    }

    private WebApplicationFactory<Program> WithProvider(IAiInventoryDraftProvider provider) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiInventoryDraftProvider>();
            services.AddSingleton(provider);
        }));

    private static async Task<int> InventoryTotalAsync(HttpClient client)
    {
        var inventory = await client.GetFromJsonAsync<PagedResult<InventoryItemResponse>>(
            "/api/inventory?page=1&pageSize=1");
        return inventory?.Total ?? throw new InvalidOperationException("Inventory response was empty.");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var anonymousToken = await GetAntiforgeryTokenAsync(client);
        using var login = AuthorizedJson(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest(email, StockPilotFactory.Password),
            anonymousToken);
        (await client.SendAsync(login)).EnsureSuccessStatusCode();
        return await GetAntiforgeryTokenAsync(client);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("Antiforgery token was empty.");
    }

    private static HttpRequestMessage AuthorizedJson<T>(
        HttpMethod method,
        string path,
        T body,
        string csrf)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class FakeDraftProvider(AiInventoryDraftCandidate candidate)
        : IAiInventoryDraftProvider
    {
        public bool IsAvailable => true;
        public string ProviderName => "Fake";
        public string? UnavailableReason => null;

        public Task<AiInventoryDraftCandidate> GenerateAsync(
            string description,
            CancellationToken cancellationToken) => Task.FromResult(candidate);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailingDraftProvider : IAiInventoryDraftProvider
    {
        public bool IsAvailable => true;
        public string ProviderName => "Fake";
        public string? UnavailableReason => null;

        public Task<AiInventoryDraftCandidate> GenerateAsync(
            string description,
            CancellationToken cancellationToken) =>
            throw new AiProviderException("provider-internal-secret");
    }
}
