using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InventoryApi.Tests;

public sealed class AuthenticationIntegrationTests : IClassFixture<StockPilotFactory>
{
    private readonly StockPilotFactory _factory;

    public AuthenticationIntegrationTests(StockPilotFactory factory) => _factory = factory;

    [Fact]
    public async Task Protected_endpoint_without_cookie_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/inventory");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_rejected()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "manager@test.local",
            StockPilotFactory.Password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_inventory_write_without_antiforgery_token_is_rejected()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "manager@test.local");

        var response = await client.PostAsJsonAsync(
            "/api/inventory",
            ValidItem($"NO-CSRF-{Guid.NewGuid():N}"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "antiforgery_failed");
    }

    [Fact]
    public async Task Viewer_cannot_create_inventory()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "viewer@test.local");

        using var request = AuthorizedJson(
            HttpMethod.Post,
            "/api/inventory",
            ValidItem("VIEWER-DENIED"),
            csrf);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_create_but_cannot_archive_inventory()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        using var createRequest = AuthorizedJson(
            HttpMethod.Post,
            "/api/inventory",
            ValidItem($"MANAGER-{Guid.NewGuid():N}"),
            csrf);

        var created = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var itemId = body.RootElement.GetProperty("id").GetGuid();

        using var archiveRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/inventory/{itemId}");
        archiveRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        var archived = await client.SendAsync(archiveRequest);

        Assert.Equal(HttpStatusCode.Forbidden, archived.StatusCode);
    }

    [Fact]
    public async Task Admin_can_access_team_management()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Item_from_another_workspace_returns_404()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "manager@test.local");

        var response = await client.GetAsync($"/api/inventory/{StockPilotFactory.OtherWorkspaceItemId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mutation_of_another_workspaces_item_returns_404()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "manager@test.local");
        using var request = AuthorizedJson(
            HttpMethod.Put,
            $"/api/inventory/{StockPilotFactory.OtherWorkspaceItemId}",
            ValidItem("OTHER-001"),
            csrf);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    public async Task Oversized_request_returns_stable_problem()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(new string('x', 65 * 1024))
        };
        request.Content.Headers.ContentType = new("application/json");

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "request_too_large");
    }

    [Fact]
    public async Task Production_disables_swagger_and_sets_security_headers()
    {
        using var productionFactory = ProductionFactory();
        using var client = productionFactory.CreateClient();
        using var scope = productionFactory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var swagger = await client.GetAsync("/swagger/index.html");
        var health = await client.GetAsync("/api/health");

        Assert.False(configuration.GetValue<bool>("SeedDemoData"));
        Assert.False(configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("script-src 'self'", health.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(
            "camera=(), microphone=(), geolocation=()",
            health.Headers.GetValues("Permissions-Policy").Single());
    }

    [Fact]
    public async Task Health_endpoints_distinguish_liveness_and_database_readiness()
    {
        using var client = _factory.CreateClient();

        var live = await client.GetFromJsonAsync<JsonElement>("/api/health/live");
        var ready = await client.GetFromJsonAsync<JsonElement>("/api/health");

        Assert.Equal("healthy", live.GetProperty("status").GetString());
        Assert.False(live.TryGetProperty("database", out _));
        Assert.Equal("healthy", ready.GetProperty("status").GetString());
        Assert.Equal("available", ready.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Authentication_persists_shared_data_protection_keys()
    {
        using var client = _factory.CreateClient();

        await LoginAsync(client, "manager@test.local");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotEmpty(await db.DataProtectionKeys.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Production_https_policy_redirects_http_and_sets_hsts()
    {
        using var productionFactory = ProductionFactory(enforceHttps: true);
        using var httpClient = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://stockpilot.test")
        });
        using var httpsClient = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://stockpilot.test")
        });

        var redirect = await httpClient.GetAsync("/api/health/live");
        var secure = await httpsClient.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.PermanentRedirect, redirect.StatusCode);
        Assert.Equal("https", redirect.Headers.Location?.Scheme);
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
        Assert.Contains(
            "max-age=31536000",
            secure.Headers.GetValues("Strict-Transport-Security").Single());
    }

    [Fact]
    public async Task Five_failed_passwords_lock_the_account()
    {
        using var client = _factory.CreateClient();
        var csrf = await GetAntiforgeryTokenAsync(client);
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            response?.Dispose();
            using var request = AuthorizedJson(
                HttpMethod.Post,
                "/api/auth/login",
                new LoginRequest("lockout@test.local", "Incorrect123!"),
                csrf);
            response = await client.SendAsync(request);
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("temporarily locked", await response.Content.ReadAsStringAsync());
        response.Dispose();

        using var validPassword = AuthorizedJson(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest("lockout@test.local", StockPilotFactory.Password),
            csrf);
        var lockedResponse = await client.SendAsync(validPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task Login_rate_limit_returns_stable_problem()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:LoginRateLimit"] = "1"
                })));
        using var client = limitedFactory.CreateClient();
        var csrf = await GetAntiforgeryTokenAsync(client);

        using var firstRequest = AuthorizedJson(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest("manager@test.local", "Incorrect123!"),
            csrf);
        var first = await client.SendAsync(firstRequest);
        using var secondRequest = AuthorizedJson(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest("manager@test.local", "Incorrect123!"),
            csrf);
        var second = await client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        await AssertProblemAsync(second, HttpStatusCode.TooManyRequests, "rate_limit_exceeded");
    }

    [Fact]
    public async Task Database_role_change_applies_to_an_existing_cookie()
    {
        using var memberClient = _factory.CreateClient();
        var memberCsrf = await LoginAsync(memberClient, "mutable@test.local");
        using var adminClient = _factory.CreateClient();
        var adminCsrf = await LoginAsync(adminClient, "admin@test.local");
        using var changeRole = AuthorizedJson(
            HttpMethod.Patch,
            $"/api/users/{StockPilotFactory.MutableUserId}/role",
            new ChangeRoleRequest(AppRoles.Viewer),
            adminCsrf);

        var changed = await adminClient.SendAsync(changeRole);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using var create = AuthorizedJson(
            HttpMethod.Post,
            "/api/inventory",
            ValidItem($"STALE-COOKIE-{Guid.NewGuid():N}"),
            memberCsrf);
        var response = await memberClient.SendAsync(create);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Removed_member_gets_401_from_me_and_can_sign_out()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "revokable@test.local");
        _factory.RemoveMembership(StockPilotFactory.RevokableUserId);

        var currentUser = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, currentUser.StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("X-CSRF-TOKEN", csrf);
        var signedOut = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
    }

    [Fact]
    public async Task Invalid_query_returns_stable_validation_problem()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "manager@test.local");

        var response = await client.GetAsync("/api/inventory?pageSize=101&minQuantity=10&maxQuantity=2");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Missing_item_returns_stable_not_found_problem()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "manager@test.local");

        var response = await client.GetAsync($"/api/inventory/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    public async Task Viewer_archived_access_returns_stable_permission_problem()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, "viewer@test.local");

        var response = await client.GetAsync("/api/inventory/archived");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "permission_denied");
    }

    [Fact]
    public async Task Admin_can_archive_discover_and_restore_inventory()
    {
        using var client = _factory.CreateClient();
        var csrf = await LoginAsync(client, "admin@test.local");
        var sku = $"RESTORE-{Guid.NewGuid():N}";
        using var create = AuthorizedJson(
            HttpMethod.Post,
            "/api/inventory",
            ValidItem(sku),
            csrf);
        var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var itemId = createdBody.RootElement.GetProperty("id").GetGuid();

        using var duplicate = AuthorizedJson(
            HttpMethod.Post,
            "/api/inventory",
            ValidItem(sku),
            csrf);
        await AssertProblemAsync(
            await client.SendAsync(duplicate),
            HttpStatusCode.Conflict,
            "duplicate_sku");

        using var archive = new HttpRequestMessage(HttpMethod.Delete, $"/api/inventory/{itemId}");
        archive.Headers.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(archive)).StatusCode);

        var archived = await client.GetAsync($"/api/inventory/archived?search={sku}");
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using var archivedBody = JsonDocument.Parse(await archived.Content.ReadAsStringAsync());
        Assert.Equal(
            itemId,
            archivedBody.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());

        using var restore = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/inventory/{itemId}/restore");
        restore.Headers.Add("X-CSRF-TOKEN", csrf);
        var restored = await client.SendAsync(restore);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        using var restoredBody = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
        Assert.False(restoredBody.RootElement.GetProperty("isArchived").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inventory/{itemId}")).StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var anonymousToken = await GetAntiforgeryTokenAsync(client);
        using var login = AuthorizedJson(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest(email, StockPilotFactory.Password),
            anonymousToken);
        var response = await client.SendAsync(login);
        response.EnsureSuccessStatusCode();
        return await GetAntiforgeryTokenAsync(client);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The antiforgery response did not contain a token.");
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
    }

    private static HttpRequestMessage AuthorizedJson<T>(
        HttpMethod method,
        string path,
        T body,
        string csrf)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private static SaveInventoryItemRequest ValidItem(string sku) => new()
    {
        Name = "Integration item",
        Sku = sku,
        Category = "Integration",
        Description = "Created by an authorization integration test.",
        Location = "TEST",
        Supplier = "Test supplier",
        Quantity = 2,
        ReorderLevel = 1,
        PurchasePrice = 10m,
        SellingPrice = 15m,
        LifecycleStatus = InventoryLifecycleStatus.Active,
        ProcurementStatus = ProcurementStatus.None
    };

    private WebApplicationFactory<Program> ProductionFactory(bool enforceHttps = false) =>
        _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\MSSQLLocalDB;Database=StockPilotIntegrationPlaceholder;Trusted_Connection=True")
            .UseSetting("SkipStartupDatabaseInitialization", "true")
            .UseSetting("Deployment:EnforceHttps", enforceHttps.ToString()));
}

public sealed class StockPilotFactory : WebApplicationFactory<Program>
{
    public const string Password = "Integration123!";
    public static readonly Guid WorkspaceId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherWorkspaceId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    public static readonly Guid OtherWorkspaceItemId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    public static readonly Guid MutableUserId = Guid.Parse("70000000-0000-0000-0000-000000000004");
    public static readonly Guid RevokableUserId = Guid.Parse("70000000-0000-0000-0000-000000000005");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["SkipStartupDatabaseInitialization"] = "true",
                ["Authentication:LoginRateLimit"] = "100",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=StockPilotIntegrationPlaceholder;Trusted_Connection=True"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        if (!db.Workspaces.Any())
            Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>());
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private static void Seed(
        AppDbContext db,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        var primary = new Workspace
        {
            Id = WorkspaceId,
            Name = "Integration workspace",
            Slug = "integration",
            CurrencyCode = "USD"
        };
        var other = new Workspace
        {
            Id = OtherWorkspaceId,
            Name = "Other workspace",
            Slug = "other",
            CurrencyCode = "USD"
        };
        db.Workspaces.AddRange(primary, other);

        AddUser(db, passwordHasher, primary, "admin@test.local", AppRoles.Admin);
        AddUser(db, passwordHasher, primary, "manager@test.local", AppRoles.Manager);
        AddUser(db, passwordHasher, primary, "viewer@test.local", AppRoles.Viewer);
        AddUser(db, passwordHasher, primary, "lockout@test.local", AppRoles.Viewer);
        AddUser(
            db,
            passwordHasher,
            primary,
            "mutable@test.local",
            AppRoles.Manager,
            MutableUserId);
        AddUser(
            db,
            passwordHasher,
            primary,
            "revokable@test.local",
            AppRoles.Viewer,
            RevokableUserId);

        var category = new Category
        {
            Workspace = other,
            Name = "Private",
            NormalizedName = "PRIVATE"
        };
        db.InventoryItems.Add(new InventoryItem
        {
            Id = OtherWorkspaceItemId,
            Workspace = other,
            Category = category,
            Name = "Other workspace item",
            Sku = "OTHER-001",
            NormalizedSku = "OTHER-001",
            Quantity = 5,
            ReorderLevel = 1,
            PurchasePrice = 2m,
            SellingPrice = 3m
        });
        db.SaveChanges();
    }

    private static void AddUser(
        AppDbContext db,
        IPasswordHasher<ApplicationUser> passwordHasher,
        Workspace workspace,
        string email,
        string role,
        Guid? id = null)
    {
        var user = new ApplicationUser
        {
            Id = id ?? Guid.NewGuid(),
            Name = role + " Tester",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            LockoutEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, Password);
        db.Users.Add(user);
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Workspace = workspace,
            User = user,
            Role = role
        });
    }

    public void RemoveMembership(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = db.WorkspaceMembers.Single(member => member.UserId == userId);
        db.WorkspaceMembers.Remove(membership);
        db.SaveChanges();
    }
}
