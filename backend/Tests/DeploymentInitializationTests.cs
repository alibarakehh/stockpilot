using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryApi.Tests;

public sealed class DeploymentInitializationTests
{
    private const string AdminEmail = "owner@stockpilot.test";
    private const string AdminPassword = "Bootstrap123!";

    [Fact]
    public async Task Bootstrap_creates_one_working_admin_and_is_idempotent()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        await using var services = BuildServices(database, new Dictionary<string, string?>
        {
            ["Bootstrap:AdminEmail"] = AdminEmail,
            ["Bootstrap:AdminPassword"] = AdminPassword,
            ["Bootstrap:AdminName"] = "Inventory Owner",
            ["Bootstrap:WorkspaceName"] = "North Warehouse",
            ["Bootstrap:WorkspaceSlug"] = "North-Warehouse",
            ["Bootstrap:CurrencyCode"] = "eur"
        });
        await CreateSchemaAsync(services);

        await SeedData.BootstrapAdminAsync(services);
        await SeedData.BootstrapAdminAsync(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workspace = Assert.Single(await db.Workspaces.AsNoTracking().ToListAsync());
        var member = Assert.Single(await db.WorkspaceMembers.AsNoTracking().ToListAsync());
        var user = Assert.Single(await db.Users.AsNoTracking().ToListAsync());
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal("North Warehouse", workspace.Name);
        Assert.Equal("north-warehouse", workspace.Slug);
        Assert.Equal("EUR", workspace.CurrencyCode);
        Assert.Equal(AppRoles.Admin, member.Role);
        Assert.Equal(AdminEmail, user.Email);
        Assert.True(await userManager.CheckPasswordAsync(user, AdminPassword));
    }

    [Fact]
    public async Task Bootstrap_without_required_secrets_writes_nothing()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        await using var services = BuildServices(database, new Dictionary<string, string?>());
        await CreateSchemaAsync(services);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedData.BootstrapAdminAsync(services));

        Assert.Contains("Bootstrap:AdminEmail", error.Message, StringComparison.Ordinal);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Workspaces.AsNoTracking().ToListAsync());
        Assert.Empty(await db.Users.AsNoTracking().ToListAsync());
        Assert.Empty(await db.WorkspaceMembers.AsNoTracking().ToListAsync());
    }

    private static ServiceProvider BuildServices(
        SqliteConnection database,
        IReadOnlyDictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build());
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(database));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }

    private static async Task CreateSchemaAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
