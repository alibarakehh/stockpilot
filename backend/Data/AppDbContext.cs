using InventoryApi.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Workspace>(entity =>
        {
            entity.HasIndex(workspace => workspace.Slug).IsUnique();
            if (Database.IsSqlServer())
            {
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Workspaces_CurrencyCode",
                    "LEN([CurrencyCode]) = 3"));
            }
        });

        builder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(member => new { member.WorkspaceId, member.UserId });
            entity.HasIndex(member => member.UserId);
            entity.HasOne(member => member.Workspace)
                .WithMany(workspace => workspace.Members)
                .HasForeignKey(member => member.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(member => member.User)
                .WithMany(user => user.WorkspaceMemberships)
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_WorkspaceMembers_Role",
                "[Role] IN ('Admin', 'Manager', 'Viewer')"));
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(category => new { category.WorkspaceId, category.NormalizedName }).IsUnique();
            entity.HasOne(category => category.Workspace)
                .WithMany(workspace => workspace.Categories)
                .HasForeignKey(category => category.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.HasIndex(item => new { item.WorkspaceId, item.NormalizedSku })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.WorkspaceId, item.UpdatedAtUtc });
            entity.Property(item => item.PurchasePrice).HasPrecision(18, 2);
            entity.Property(item => item.SellingPrice).HasPrecision(18, 2);
            entity.Property(item => item.LifecycleStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.ProcurementStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasQueryFilter(item => !item.IsDeleted);
            entity.HasOne(item => item.Workspace)
                .WithMany(workspace => workspace.InventoryItems)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Category)
                .WithMany(category => category.InventoryItems)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Movements)
                .WithOne(movement => movement.InventoryItem)
                .HasForeignKey(movement => movement.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_InventoryItems_Quantity", "[Quantity] >= 0");
                table.HasCheckConstraint("CK_InventoryItems_ReorderLevel", "[ReorderLevel] >= 0");
                table.HasCheckConstraint("CK_InventoryItems_PurchasePrice", "[PurchasePrice] >= 0");
                table.HasCheckConstraint("CK_InventoryItems_SellingPrice", "[SellingPrice] >= 0");
                table.HasCheckConstraint(
                    "CK_InventoryItems_LifecycleStatus",
                    "[LifecycleStatus] IN ('Active', 'Discontinued')");
                table.HasCheckConstraint(
                    "CK_InventoryItems_ProcurementStatus",
                    "[ProcurementStatus] IN ('None', 'Ordered')");
            });
        });

        builder.Entity<InventoryMovement>(entity =>
        {
            entity.HasQueryFilter(movement => !movement.InventoryItem.IsDeleted);
            entity.Property(movement => movement.Type).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(movement => new { movement.WorkspaceId, movement.RequestId }).IsUnique();
            entity.HasIndex(movement => new { movement.WorkspaceId, movement.CreatedAtUtc });
            entity.HasIndex(movement => new { movement.InventoryItemId, movement.CreatedAtUtc });
            entity.HasOne(movement => movement.Workspace)
                .WithMany()
                .HasForeignKey(movement => movement.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_InventoryMovements_Balance",
                "[PreviousQuantity] >= 0 AND [NewQuantity] >= 0 " +
                "AND [Change] <> 0 AND [NewQuantity] - [PreviousQuantity] = [Change]"));
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.HasIndex(audit => new { audit.WorkspaceId, audit.CreatedAtUtc });
            entity.HasIndex(audit => new { audit.WorkspaceId, audit.EntityType, audit.EntityId });
            entity.HasOne(audit => audit.Workspace)
                .WithMany()
                .HasForeignKey(audit => audit.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Name).HasMaxLength(120);
            entity.Property(user => user.Email).HasMaxLength(200);
        });
    }
}
