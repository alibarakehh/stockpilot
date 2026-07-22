using System.Linq.Expressions;
using System.Text.Json;
using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Services;

public sealed class InventoryService(AppDbContext db) : IInventoryService
{
    public async Task<PagedResult<InventoryItemResponse>> SearchAsync(
        Guid workspaceId,
        InventoryQuery query,
        CancellationToken cancellationToken) =>
        await SearchCoreAsync(
            db.InventoryItems.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId),
            query,
            cancellationToken);

    public async Task<PagedResult<InventoryItemResponse>> SearchArchivedAsync(
        Guid workspaceId,
        InventoryQuery query,
        CancellationToken cancellationToken) =>
        await SearchCoreAsync(
            db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.IsDeleted),
            query,
            cancellationToken);

    private static async Task<PagedResult<InventoryItemResponse>> SearchCoreAsync(
        IQueryable<InventoryItem> items,
        InventoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            items = items.Where(item =>
                EF.Functions.Like(item.Name, term) ||
                EF.Functions.Like(item.Sku, term) ||
                EF.Functions.Like(item.Category.Name, term) ||
                EF.Functions.Like(item.Location, term) ||
                EF.Functions.Like(item.Supplier, term) ||
                EF.Functions.Like(item.Description, term));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var normalizedCategory = NormalizeName(query.Category);
            items = items.Where(item => item.Category.NormalizedName == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(query.Supplier))
        {
            var supplier = $"%{query.Supplier.Trim()}%";
            items = items.Where(item => EF.Functions.Like(item.Supplier, supplier));
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var location = $"%{query.Location.Trim()}%";
            items = items.Where(item => EF.Functions.Like(item.Location, location));
        }

        if (query.MinQuantity is not null)
            items = items.Where(item => item.Quantity >= query.MinQuantity.Value);
        if (query.MaxQuantity is not null)
            items = items.Where(item => item.Quantity <= query.MaxQuantity.Value);

        if (query.Status is not null) items = ApplyStatusFilter(items, query.Status.Value);

        items = ApplySort(items, query.SortBy, query.Descending);
        var total = await items.CountAsync(cancellationToken);
        var page = await items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryItemResponse>(page, total, query.Page, query.PageSize);
    }

    public async Task<InventoryItemResponse?> GetAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken) =>
        await db.InventoryItems.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Id == id)
            .Select(ResponseProjection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<InventorySummaryResponse> GetSummaryAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var currencyCode = await db.Workspaces.AsNoTracking()
            .Where(workspace => workspace.Id == workspaceId)
            .Select(workspace => workspace.CurrencyCode)
            .SingleAsync(cancellationToken);
        var totals = await db.InventoryItems.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .GroupBy(_ => 1)
            .Select(items => new
            {
                TotalItems = items.Count(),
                TotalUnits = items.Sum(item => item.Quantity),
                InStockCount = items.Count(item =>
                    item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                    item.ProcurementStatus == ProcurementStatus.None &&
                    item.Quantity > item.ReorderLevel),
                LowStockCount = items.Count(item =>
                    item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                    item.ProcurementStatus == ProcurementStatus.None &&
                    item.Quantity > 0 && item.Quantity <= item.ReorderLevel),
                OutOfStockCount = items.Count(item =>
                    item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                    item.ProcurementStatus == ProcurementStatus.None &&
                    item.Quantity == 0),
                OrderedCount = items.Count(item =>
                    item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                    item.ProcurementStatus == ProcurementStatus.Ordered),
                DiscontinuedCount = items.Count(item =>
                    item.LifecycleStatus == InventoryLifecycleStatus.Discontinued)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalValue = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (decimal)await db.InventoryItems.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId)
                .SumAsync(
                    item => (double)item.Quantity * (double)item.PurchasePrice,
                    cancellationToken)
            : await db.InventoryItems.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId)
                .SumAsync(item => item.Quantity * item.PurchasePrice, cancellationToken);

        return new InventorySummaryResponse(
            totals?.TotalItems ?? 0,
            totals?.TotalUnits ?? 0,
            totalValue,
            currencyCode,
            totals?.InStockCount ?? 0,
            totals?.LowStockCount ?? 0,
            totals?.OutOfStockCount ?? 0,
            totals?.OrderedCount ?? 0,
            totals?.DiscontinuedCount ?? 0);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .Where(category => category.WorkspaceId == workspaceId)
            .OrderBy(category => category.Name)
            .Select(category => category.Name)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<InventoryMovementResponse>> GetMovementsAsync(
        Guid workspaceId,
        MovementQuery query,
        CancellationToken cancellationToken)
    {
        var movementsQuery = db.InventoryMovements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(movement => movement.WorkspaceId == workspaceId);
        if (query.ItemId is not null)
            movementsQuery = movementsQuery.Where(movement => movement.InventoryItemId == query.ItemId);

        var total = await movementsQuery.CountAsync(cancellationToken);
        var movements = await movementsQuery
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(movement => new InventoryMovementResponse(
                movement.Id,
                movement.RequestId,
                movement.InventoryItemId,
                movement.InventoryItem.Name,
                movement.InventoryItem.Sku,
                movement.Type,
                movement.Change,
                movement.PreviousQuantity,
                movement.NewQuantity,
                movement.Reason,
                movement.PerformedByName,
                movement.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryMovementResponse>(
            movements,
            total,
            query.Page,
            query.PageSize);
    }

    public async Task<InventoryItemResponse> CreateAsync(
        SaveInventoryItemRequest request,
        StockActor actor,
        CancellationToken cancellationToken)
    {
        var sku = NormalizeSku(request.Sku);
        if (await db.InventoryItems.AnyAsync(
                item => item.WorkspaceId == actor.WorkspaceId && item.NormalizedSku == sku,
                cancellationToken))
        {
            throw new DuplicateSkuException(sku);
        }

        var category = await GetOrCreateCategoryAsync(actor.WorkspaceId, request.Category, cancellationToken);
        var workspace = await db.Workspaces.SingleAsync(
            candidate => candidate.Id == actor.WorkspaceId,
            cancellationToken);
        var lifecycle = request.LifecycleStatus;
        var procurement = lifecycle == InventoryLifecycleStatus.Discontinued
            ? ProcurementStatus.None
            : request.ProcurementStatus;
        var item = new InventoryItem
        {
            WorkspaceId = actor.WorkspaceId,
            Workspace = workspace,
            CategoryId = category.Id,
            Category = category,
            Name = request.Name.Trim(),
            Sku = sku,
            NormalizedSku = sku,
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Supplier = request.Supplier.Trim(),
            Quantity = request.Quantity,
            ReorderLevel = request.ReorderLevel,
            PurchasePrice = request.PurchasePrice,
            SellingPrice = request.SellingPrice,
            LifecycleStatus = lifecycle,
            ProcurementStatus = procurement,
            CreatedByUserId = actor.UserId,
            UpdatedByUserId = actor.UserId
        };
        db.InventoryItems.Add(item);

        if (item.Quantity > 0)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                WorkspaceId = actor.WorkspaceId,
                InventoryItemId = item.Id,
                RequestId = Guid.NewGuid(),
                Type = MovementType.OpeningBalance,
                Change = item.Quantity,
                PreviousQuantity = 0,
                NewQuantity = item.Quantity,
                Reason = "Opening balance",
                PerformedByUserId = actor.UserId,
                PerformedByName = actor.Name
            });
        }
        AddAudit(item.Id, "Created", actor, new { item.Sku, item.Quantity });
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<InventoryItemResponse?> UpdateAsync(
        Guid id,
        SaveInventoryItemRequest request,
        StockActor actor,
        CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Workspace)
            .SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == actor.WorkspaceId && candidate.Id == id,
                cancellationToken);
        if (item is null) return null;
        if (request.Version is not null && request.Version != item.Version)
            throw new InventoryConcurrencyException();
        if (request.Quantity != item.Quantity) throw new QuantityRequiresAdjustmentException();

        var sku = NormalizeSku(request.Sku);
        if (await db.InventoryItems.AnyAsync(
                candidate => candidate.WorkspaceId == actor.WorkspaceId &&
                    candidate.NormalizedSku == sku && candidate.Id != id,
                cancellationToken))
        {
            throw new DuplicateSkuException(sku);
        }

        var category = await GetOrCreateCategoryAsync(actor.WorkspaceId, request.Category, cancellationToken);
        item.Name = request.Name.Trim();
        item.Sku = sku;
        item.NormalizedSku = sku;
        item.CategoryId = category.Id;
        item.Category = category;
        item.Description = request.Description.Trim();
        item.Location = request.Location.Trim();
        item.Supplier = request.Supplier.Trim();
        item.ReorderLevel = request.ReorderLevel;
        item.PurchasePrice = request.PurchasePrice;
        item.SellingPrice = request.SellingPrice;
        item.LifecycleStatus = request.LifecycleStatus;
        item.ProcurementStatus = request.LifecycleStatus == InventoryLifecycleStatus.Discontinued
            ? ProcurementStatus.None
            : request.ProcurementStatus;
        item.UpdatedByUserId = actor.UserId;
        item.Version++;
        item.UpdatedAtUtc = DateTime.UtcNow;
        AddAudit(item.Id, "Updated", actor, new
        {
            item.Sku,
            item.LifecycleStatus,
            item.ProcurementStatus,
            item.Version
        });

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<InventoryItemResponse?> AdjustStockAsync(
        Guid id,
        StockAdjustmentRequest request,
        StockActor actor,
        CancellationToken cancellationToken)
    {
        ValidateMovement(request);
        var priorMovement = await db.InventoryMovements.AsNoTracking()
            .SingleOrDefaultAsync(
                movement => movement.WorkspaceId == actor.WorkspaceId &&
                    movement.RequestId == request.RequestId,
                cancellationToken);
        if (priorMovement is not null)
        {
            if (priorMovement.InventoryItemId != id ||
                priorMovement.Change != request.Change ||
                priorMovement.Type != request.Type ||
                priorMovement.Reason != request.Reason.Trim())
            {
                throw new IdempotencyConflictException();
            }
            return await GetAsync(actor.WorkspaceId, id, cancellationToken);
        }

        var item = await db.InventoryItems
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Workspace)
            .SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == actor.WorkspaceId && candidate.Id == id,
                cancellationToken);
        if (item is null) return null;
        if (item.LifecycleStatus == InventoryLifecycleStatus.Discontinued)
            throw new DiscontinuedItemException();
        if (request.Version is not null && request.Version != item.Version)
            throw new InventoryConcurrencyException();

        var previousQuantity = item.Quantity;
        var newQuantity = previousQuantity + request.Change;
        if (newQuantity < 0) throw new InvalidStockAdjustmentException();

        item.Quantity = newQuantity;
        if (request.Type == MovementType.Receipt) item.ProcurementStatus = ProcurementStatus.None;
        item.UpdatedByUserId = actor.UserId;
        item.Version++;
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.InventoryMovements.Add(new InventoryMovement
        {
            WorkspaceId = actor.WorkspaceId,
            InventoryItemId = item.Id,
            RequestId = request.RequestId,
            Type = request.Type,
            Change = request.Change,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            Reason = request.Reason.Trim(),
            PerformedByUserId = actor.UserId,
            PerformedByName = actor.Name
        });
        AddAudit(item.Id, "StockAdjusted", actor, new
        {
            request.RequestId,
            request.Type,
            request.Change,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var resolved = await TryResolveIdempotentRetryAsync(
                id,
                request,
                actor.WorkspaceId,
                cancellationToken);
            if (resolved is not null) return resolved;
            throw;
        }
        catch (DbUpdateException)
        {
            var resolved = await TryResolveIdempotentRetryAsync(
                id,
                request,
                actor.WorkspaceId,
                cancellationToken);
            if (resolved is not null) return resolved;
            throw;
        }
        return ToResponse(item);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        StockActor actor,
        CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.SingleOrDefaultAsync(
            candidate => candidate.WorkspaceId == actor.WorkspaceId && candidate.Id == id,
            cancellationToken);
        if (item is null) return false;
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        item.DeletedByUserId = actor.UserId;
        item.UpdatedByUserId = actor.UserId;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.Version++;
        AddAudit(item.Id, "Archived", actor, new { item.Sku, item.Version });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<InventoryItemResponse?> RestoreAsync(
        Guid id,
        StockActor actor,
        CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Workspace)
            .SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == actor.WorkspaceId &&
                    candidate.Id == id,
                cancellationToken);
        if (item is null) return null;
        if (!item.IsDeleted) return ToResponse(item);

        if (await db.InventoryItems.AnyAsync(
                candidate => candidate.WorkspaceId == actor.WorkspaceId &&
                    candidate.NormalizedSku == item.NormalizedSku,
                cancellationToken))
        {
            throw new DuplicateSkuException(item.Sku);
        }

        item.IsDeleted = false;
        item.DeletedAtUtc = null;
        item.DeletedByUserId = null;
        item.UpdatedByUserId = actor.UserId;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.Version++;
        AddAudit(item.Id, "Restored", actor, new { item.Sku, item.Version });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var winner = await db.InventoryItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(candidate => candidate.WorkspaceId == actor.WorkspaceId &&
                    candidate.Id == id && !candidate.IsDeleted)
                .Select(ResponseProjection)
                .SingleOrDefaultAsync(cancellationToken);
            if (winner is not null) return winner;
            throw;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.InventoryItems.AnyAsync(
                    candidate => candidate.WorkspaceId == actor.WorkspaceId &&
                        candidate.NormalizedSku == item.NormalizedSku && candidate.Id != id,
                    cancellationToken))
            {
                throw new DuplicateSkuException(item.Sku);
            }
            throw;
        }
        return ToResponse(item);
    }

    private async Task<Category> GetOrCreateCategoryAsync(
        Guid workspaceId,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var name = categoryName.Trim();
        var normalizedName = NormalizeName(name);
        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.WorkspaceId == workspaceId &&
                candidate.NormalizedName == normalizedName,
            cancellationToken);
        if (category is not null) return category;

        category = new Category
        {
            WorkspaceId = workspaceId,
            Name = name,
            NormalizedName = normalizedName
        };
        db.Categories.Add(category);
        return category;
    }

    private async Task<InventoryItemResponse?> TryResolveIdempotentRetryAsync(
        Guid itemId,
        StockAdjustmentRequest request,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var movement = await db.InventoryMovements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == workspaceId &&
                    candidate.RequestId == request.RequestId,
                cancellationToken);
        if (movement is null) return null;
        if (movement.InventoryItemId != itemId ||
            movement.Change != request.Change ||
            movement.Type != request.Type ||
            movement.Reason != request.Reason.Trim())
        {
            throw new IdempotencyConflictException();
        }
        return await GetAsync(workspaceId, itemId, cancellationToken);
    }

    private void AddAudit(Guid entityId, string action, StockActor actor, object details) =>
        db.AuditEvents.Add(new AuditEvent
        {
            WorkspaceId = actor.WorkspaceId,
            EntityType = nameof(InventoryItem),
            EntityId = entityId,
            Action = action,
            ActorUserId = actor.UserId,
            ActorName = actor.Name,
            DetailsJson = JsonSerializer.Serialize(details)
        });

    private static IQueryable<InventoryItem> ApplySort(
        IQueryable<InventoryItem> query,
        string sortBy,
        bool descending) =>
        (sortBy.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(item => item.Name),
            ("name", true) => query.OrderByDescending(item => item.Name),
            ("quantity", false) => query.OrderBy(item => item.Quantity),
            ("quantity", true) => query.OrderByDescending(item => item.Quantity),
            ("sku", false) => query.OrderBy(item => item.Sku),
            ("sku", true) => query.OrderByDescending(item => item.Sku),
            ("category", false) => query.OrderBy(item => item.Category.Name),
            ("category", true) => query.OrderByDescending(item => item.Category.Name),
            ("purchaseprice", false) => query.OrderBy(item => item.PurchasePrice),
            ("purchaseprice", true) => query.OrderByDescending(item => item.PurchasePrice),
            ("sellingprice", false) => query.OrderBy(item => item.SellingPrice),
            ("sellingprice", true) => query.OrderByDescending(item => item.SellingPrice),
            ("location", false) => query.OrderBy(item => item.Location),
            ("location", true) => query.OrderByDescending(item => item.Location),
            ("supplier", false) => query.OrderBy(item => item.Supplier),
            ("supplier", true) => query.OrderByDescending(item => item.Supplier),
            ("value", false) => query.OrderBy(item => (double)item.Quantity * (double)item.PurchasePrice),
            ("value", true) => query.OrderByDescending(item => (double)item.Quantity * (double)item.PurchasePrice),
            (_, false) => query.OrderBy(item => item.UpdatedAtUtc),
            _ => query.OrderByDescending(item => item.UpdatedAtUtc)
        };

    private static IQueryable<InventoryItem> ApplyStatusFilter(
        IQueryable<InventoryItem> query,
        StockStatus status) => status switch
        {
            StockStatus.Discontinued => query.Where(item =>
                item.LifecycleStatus == InventoryLifecycleStatus.Discontinued),
            StockStatus.Ordered => query.Where(item =>
                item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                item.ProcurementStatus == ProcurementStatus.Ordered),
            StockStatus.OutOfStock => query.Where(item =>
                item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                item.ProcurementStatus == ProcurementStatus.None && item.Quantity == 0),
            StockStatus.LowStock => query.Where(item =>
                item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                item.ProcurementStatus == ProcurementStatus.None &&
                item.Quantity > 0 && item.Quantity <= item.ReorderLevel),
            _ => query.Where(item =>
                item.LifecycleStatus == InventoryLifecycleStatus.Active &&
                item.ProcurementStatus == ProcurementStatus.None && item.Quantity > item.ReorderLevel)
        };

    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();
    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private static void ValidateMovement(StockAdjustmentRequest request)
    {
        if (request.RequestId == Guid.Empty)
            throw new InvalidMovementDirectionException("A request identifier is required.");
        if (request.Change == 0)
            throw new InvalidMovementDirectionException("Adjustment quantity cannot be zero.");
        if (request.Type is MovementType.OpeningBalance)
            throw new InvalidMovementDirectionException("Opening balance is created only with a new item.");
        if (request.Type is MovementType.Receipt or MovementType.Return && request.Change < 0)
            throw new InvalidMovementDirectionException("Receipts and returns must add stock.");
        if (request.Type is MovementType.Issue or MovementType.Damage && request.Change > 0)
            throw new InvalidMovementDirectionException("Issues and damage must reduce stock.");
    }

    private static readonly Expression<Func<InventoryItem, InventoryItemResponse>> ResponseProjection =
        item => new InventoryItemResponse(
            item.Id,
            item.Name,
            item.Sku,
            item.Category.Name,
            item.Description,
            item.Location,
            item.Supplier,
            item.Quantity,
            item.ReorderLevel,
            item.PurchasePrice,
            item.SellingPrice,
            item.Quantity * item.PurchasePrice,
            item.Workspace.CurrencyCode,
            item.LifecycleStatus,
            item.ProcurementStatus,
            item.LifecycleStatus == InventoryLifecycleStatus.Discontinued
                ? StockStatus.Discontinued
                : item.ProcurementStatus == ProcurementStatus.Ordered
                    ? StockStatus.Ordered
                    : item.Quantity == 0
                        ? StockStatus.OutOfStock
                        : item.Quantity <= item.ReorderLevel
                            ? StockStatus.LowStock
                            : StockStatus.InStock,
            item.IsDeleted,
            item.DeletedAtUtc,
            item.Version,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);

    private static InventoryItemResponse ToResponse(InventoryItem item) => new(
        item.Id,
        item.Name,
        item.Sku,
        item.Category.Name,
        item.Description,
        item.Location,
        item.Supplier,
        item.Quantity,
        item.ReorderLevel,
        item.PurchasePrice,
        item.SellingPrice,
        item.Quantity * item.PurchasePrice,
        item.Workspace.CurrencyCode,
        item.LifecycleStatus,
        item.ProcurementStatus,
        item.Status,
        item.IsDeleted,
        item.DeletedAtUtc,
        item.Version,
        item.CreatedAtUtc,
        item.UpdatedAtUtc);
}
