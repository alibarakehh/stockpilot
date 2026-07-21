using InventoryApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Data;

public static class SeedData
{
    private static readonly Guid DemoWorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ManagerUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ViewerUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    public static async Task MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public static async Task BootstrapAdminAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.WorkspaceMembers.AsNoTracking().AnyAsync(cancellationToken)) return;

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email = RequiredBootstrapValue(configuration, "Bootstrap:AdminEmail");
        var password = RequiredBootstrapValue(configuration, "Bootstrap:AdminPassword");
        var adminName = BootstrapText(
            configuration,
            "Bootstrap:AdminName",
            "StockPilot Admin",
            2,
            120);
        var workspaceName = BootstrapText(
            configuration,
            "Bootstrap:WorkspaceName",
            "StockPilot",
            2,
            120);
        var workspaceSlug = BootstrapText(
                configuration,
                "Bootstrap:WorkspaceSlug",
                "stockpilot",
                2,
                80)
            .ToLowerInvariant();
        if (!workspaceSlug.All(character =>
                char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            throw new InvalidOperationException(
                "Bootstrap:WorkspaceSlug may contain only letters, numbers, and hyphens.");
        }
        var currencyCode = (configuration["Bootstrap:CurrencyCode"] ?? "USD")
            .Trim()
            .ToUpperInvariant();
        if (currencyCode.Length != 3 || !currencyCode.All(char.IsAsciiLetterUpper))
            throw new InvalidOperationException("Bootstrap:CurrencyCode must contain three letters.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var workspace = new Workspace
        {
            Name = workspaceName,
            Slug = workspaceSlug,
            CurrencyCode = currencyCode
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            Name = adminName,
            Email = email,
            UserName = email,
            LockoutEnabled = true
        };
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        EnsureSucceeded(await userManager.CreateAsync(user, password), "create the bootstrap admin");
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Role = AppRoles.Admin
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
            await db.Database.MigrateAsync(cancellationToken);
        if (!configuration.GetValue("SeedDemoData", false)) return;

        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var demoPassword = configuration["SeedDemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "SeedDemoPassword must be configured when demo data is enabled outside development.");
            }

            demoPassword = "Welcome123!";
        }

        var workspace = await db.Workspaces.SingleOrDefaultAsync(
            candidate => candidate.Id == DemoWorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            workspace = new Workspace
            {
                Id = DemoWorkspaceId,
                Name = "StockPilot Demo",
                Slug = "stockpilot-demo",
                CurrencyCode = "USD"
            };
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync(cancellationToken);
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userSeeds = new[]
        {
            new UserSeed(AdminUserId, "System Admin", "admin@stockpilot.local", AppRoles.Admin),
            new UserSeed(ManagerUserId, "Inventory Manager", "manager@stockpilot.local", AppRoles.Manager),
            new UserSeed(ViewerUserId, "Team Viewer", "viewer@stockpilot.local", AppRoles.Viewer)
        };
        foreach (var seed in userSeeds)
        {
            var user = await db.Users.SingleOrDefaultAsync(
                candidate => candidate.Id == seed.Id,
                cancellationToken);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = seed.Id,
                    Name = seed.Name,
                    Email = seed.Email,
                    UserName = seed.Email,
                    LockoutEnabled = true
                };
                EnsureSucceeded(
                    await userManager.CreateAsync(user, demoPassword),
                    $"create demo user {seed.Email}");
            }
            else
            {
                user.Name = seed.Name;
                user.Email = seed.Email;
                user.UserName = seed.Email;
                user.LockoutEnabled = true;
                user.SecurityStamp ??= Guid.NewGuid().ToString();
                EnsureSucceeded(
                    await userManager.UpdateAsync(user),
                    $"update demo user {seed.Email}");
            }

            if (!await db.WorkspaceMembers.AnyAsync(
                    member => member.WorkspaceId == DemoWorkspaceId && member.UserId == seed.Id,
                    cancellationToken))
            {
                db.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = DemoWorkspaceId,
                    UserId = seed.Id,
                    Role = seed.Role
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        var itemSeeds = new[]
        {
            new ItemSeed("Ergonomic Keyboard", "TECH-1001", "Electronics", 24, 8, 79.99m, 119.99m, "A-01", "Northstar Tech"),
            new ItemSeed("USB-C Dock", "TECH-1002", "Electronics", 4, 10, 119.00m, 169.00m, "A-02", "Northstar Tech"),
            new ItemSeed("Recycled Notebook", "OFF-2001", "Office", 62, 15, 6.50m, 11.00m, "B-04", "Paper & Co."),
            new ItemSeed("Standing Desk", "FUR-3001", "Furniture", 2, 4, 429.00m, 599.00m, "Floor 2", "ErgoWorks"),
            new ItemSeed("Thermal Labels", "OPS-4001", "Operations", 0, 20, 18.75m, 29.00m, "C-07", "PackRight", ProcurementStatus.Ordered),
            new ItemSeed("Legacy Monitor Arm", "FUR-3002", "Furniture", 0, 0, 54.00m, 70.00m, "Archive", "", ProcurementStatus.None, InventoryLifecycleStatus.Discontinued)
        };
        var categories = await db.Categories
            .Where(category => category.WorkspaceId == DemoWorkspaceId)
            .ToDictionaryAsync(category => category.NormalizedName, cancellationToken);
        foreach (var categoryName in itemSeeds.Select(seed => seed.Category).Distinct())
        {
            var normalizedName = categoryName.ToUpperInvariant();
            if (categories.ContainsKey(normalizedName)) continue;
            var category = new Category
            {
                WorkspaceId = DemoWorkspaceId,
                Name = categoryName,
                NormalizedName = normalizedName
            };
            categories.Add(normalizedName, category);
            db.Categories.Add(category);
        }
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.InventoryItems.AnyAsync(
                item => item.WorkspaceId == DemoWorkspaceId,
                cancellationToken))
        {
            foreach (var seed in itemSeeds)
            {
                var category = categories[seed.Category.ToUpperInvariant()];
                db.InventoryItems.Add(new InventoryItem
                {
                    WorkspaceId = DemoWorkspaceId,
                    CategoryId = category.Id,
                    Name = seed.Name,
                    Sku = seed.Sku,
                    NormalizedSku = seed.Sku,
                    Quantity = seed.Quantity,
                    ReorderLevel = seed.ReorderLevel,
                    PurchasePrice = seed.PurchasePrice,
                    SellingPrice = seed.SellingPrice,
                    Location = seed.Location,
                    Supplier = seed.Supplier,
                    ProcurementStatus = seed.ProcurementStatus,
                    LifecycleStatus = seed.LifecycleStatus,
                    CreatedByUserId = AdminUserId,
                    UpdatedByUserId = AdminUserId
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.InventoryMovements.AnyAsync(
                movement => movement.WorkspaceId == DemoWorkspaceId,
                cancellationToken))
        {
            var items = await db.InventoryItems
                .Where(item => item.WorkspaceId == DemoWorkspaceId && item.Quantity > 0)
                .ToListAsync(cancellationToken);
            foreach (var item in items)
            {
                db.InventoryMovements.Add(new InventoryMovement
                {
                    WorkspaceId = DemoWorkspaceId,
                    InventoryItemId = item.Id,
                    RequestId = Guid.NewGuid(),
                    Type = MovementType.OpeningBalance,
                    Change = item.Quantity,
                    PreviousQuantity = 0,
                    NewQuantity = item.Quantity,
                    Reason = "Seeded opening balance",
                    PerformedByUserId = AdminUserId,
                    PerformedByName = "StockPilot setup"
                });
            }
        }

        if (!await db.AuditEvents.AnyAsync(
                audit => audit.WorkspaceId == DemoWorkspaceId,
                cancellationToken))
        {
            db.AuditEvents.Add(new AuditEvent
            {
                WorkspaceId = DemoWorkspaceId,
                EntityType = nameof(Workspace),
                EntityId = DemoWorkspaceId,
                Action = "DemoSeeded",
                ActorUserId = AdminUserId,
                ActorName = "StockPilot setup"
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record UserSeed(Guid Id, string Name, string Email, string Role);

    private static string RequiredBootstrapValue(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!.Trim()
            : throw new InvalidOperationException($"{key} must be configured for the first deployment.");

    private static string BootstrapText(
        IConfiguration configuration,
        string key,
        string defaultValue,
        int minimumLength,
        int maximumLength)
    {
        var value = configuration[key]?.Trim() ?? defaultValue;
        if (value.Length >= minimumLength && value.Length <= maximumLength) return value;
        throw new InvalidOperationException(
            $"{key} must be between {minimumLength} and {maximumLength} characters.");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(
            $"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }

    private sealed record ItemSeed(
        string Name,
        string Sku,
        string Category,
        int Quantity,
        int ReorderLevel,
        decimal PurchasePrice,
        decimal SellingPrice,
        string Location,
        string Supplier,
        ProcurementStatus ProcurementStatus = ProcurementStatus.None,
        InventoryLifecycleStatus LifecycleStatus = InventoryLifecycleStatus.Active);
}
