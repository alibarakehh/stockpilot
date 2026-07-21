using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryApi.Tests;

public sealed class InventoryServiceTests : IAsyncDisposable
{
    private static readonly Guid WorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherWorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly StockActor Actor = new(WorkspaceId, Guid.NewGuid(), "Test Operator");
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _db.Workspaces.AddRange(
            new Workspace
            {
                Id = WorkspaceId,
                Name = "Primary workspace",
                Slug = "primary",
                CurrencyCode = "USD"
            },
            new Workspace
            {
                Id = OtherWorkspaceId,
                Name = "Other workspace",
                Slug = "other",
                CurrencyCode = "EUR"
            });
        _db.SaveChanges();
        _service = new InventoryService(_db);
    }

    [Fact]
    public async Task Create_NormalizesSkuAndClassifiesLowStock()
    {
        var item = await _service.CreateAsync(
            Request(sku: "  abc-10 ", quantity: 3, reorder: 5),
            Actor,
            default);

        Assert.Equal("ABC-10", item.Sku);
        Assert.Equal(StockStatus.LowStock, item.Status);
        Assert.Equal(30m, item.InventoryValue);
        Assert.Equal("USD", item.CurrencyCode);
    }

    [Fact]
    public async Task DerivedStatus_UsesOperationalPriority()
    {
        var outOfStock = await _service.CreateAsync(Request(sku: "OUT-1", quantity: 0), Actor, default);
        var ordered = await _service.CreateAsync(
            Request(sku: "ORD-1", quantity: 0, procurement: ProcurementStatus.Ordered),
            Actor,
            default);
        var discontinued = await _service.CreateAsync(
            Request(
                sku: "END-1",
                quantity: 0,
                procurement: ProcurementStatus.Ordered,
                lifecycle: InventoryLifecycleStatus.Discontinued),
            Actor,
            default);

        Assert.Equal(StockStatus.OutOfStock, outOfStock.Status);
        Assert.Equal(StockStatus.Ordered, ordered.Status);
        Assert.Equal(StockStatus.Discontinued, discontinued.Status);
        Assert.Equal(ProcurementStatus.None, discontinued.ProcurementStatus);
    }

    [Theory]
    [InlineData(0, 5, StockStatus.OutOfStock)]
    [InlineData(5, 5, StockStatus.LowStock)]
    [InlineData(6, 5, StockStatus.InStock)]
    public void DerivedStatus_UsesReorderBoundaries(
        int quantity,
        int reorderLevel,
        StockStatus expected)
    {
        var status = InventoryStatus.Calculate(
            InventoryLifecycleStatus.Active,
            ProcurementStatus.None,
            quantity,
            reorderLevel);

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task Create_RejectsDuplicateSkuIgnoringCasingAndWhitespace()
    {
        await _service.CreateAsync(Request(sku: "ABC-10"), Actor, default);

        await Assert.ThrowsAsync<DuplicateSkuException>(() =>
            _service.CreateAsync(Request(sku: " abc-10 "), Actor, default));
    }

    [Fact]
    public async Task Search_FiltersAcrossNameSkuCategoryAndDescription()
    {
        await _service.CreateAsync(
            Request(name: "Wireless Mouse", sku: "TECH-9", category: "Electronics"),
            Actor,
            default);
        await _service.CreateAsync(
            Request(name: "Packing Tape", sku: "OPS-4", category: "Operations"),
            Actor,
            default);

        var result = await _service.SearchAsync(
            WorkspaceId,
            new InventoryQuery { Search = "wireless" },
            default);

        Assert.Single(result.Items);
        Assert.Equal("TECH-9", result.Items[0].Sku);
    }

    [Fact]
    public async Task Search_AppliesOperationalFiltersAndSorting()
    {
        await _service.CreateAsync(
            Request(sku: "FILTER-1", quantity: 7, location: "A-01", supplier: "Northstar"),
            Actor,
            default);
        await _service.CreateAsync(
            Request(sku: "FILTER-2", quantity: 11, location: "A-02", supplier: "Northstar"),
            Actor,
            default);
        await _service.CreateAsync(
            Request(sku: "FILTER-3", quantity: 20, location: "B-01", supplier: "Other"),
            Actor,
            default);

        var result = await _service.SearchAsync(
            WorkspaceId,
            new InventoryQuery
            {
                Supplier = "north",
                Location = "A-",
                MinQuantity = 5,
                MaxQuantity = 12,
                SortBy = "quantity",
                Descending = true
            },
            default);

        Assert.Collection(
            result.Items,
            item => Assert.Equal("FILTER-2", item.Sku),
            item => Assert.Equal("FILTER-1", item.Sku));
    }

    [Fact]
    public async Task Summary_AggregatesEveryOperationalStatus()
    {
        await _service.CreateAsync(Request(sku: "SUMMARY-IN", quantity: 6, reorder: 5), Actor, default);
        await _service.CreateAsync(Request(sku: "SUMMARY-LOW", quantity: 5, reorder: 5), Actor, default);
        await _service.CreateAsync(Request(sku: "SUMMARY-OUT", quantity: 0), Actor, default);
        await _service.CreateAsync(
            Request(sku: "SUMMARY-ORDERED", quantity: 0, procurement: ProcurementStatus.Ordered),
            Actor,
            default);
        await _service.CreateAsync(
            Request(sku: "SUMMARY-END", quantity: 0, lifecycle: InventoryLifecycleStatus.Discontinued),
            Actor,
            default);

        var summary = await _service.GetSummaryAsync(WorkspaceId, default);

        Assert.Equal(5, summary.TotalItems);
        Assert.Equal(11, summary.TotalUnits);
        Assert.Equal(110m, summary.TotalValue);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.OutOfStockCount);
        Assert.Equal(1, summary.OrderedCount);
        Assert.Equal(1, summary.DiscontinuedCount);
    }

    [Fact]
    public async Task Movements_AreWorkspaceScopedAndPaginated()
    {
        await _service.CreateAsync(Request(sku: "MOVEMENT-1", quantity: 1), Actor, default);
        await _service.CreateAsync(Request(sku: "MOVEMENT-2", quantity: 2), Actor, default);
        await _service.CreateAsync(Request(sku: "MOVEMENT-3", quantity: 3), Actor, default);
        var otherActor = new StockActor(OtherWorkspaceId, Guid.NewGuid(), "Other Operator");
        await _service.CreateAsync(Request(sku: "MOVEMENT-OTHER", quantity: 4), otherActor, default);

        var first = await _service.GetMovementsAsync(
            WorkspaceId,
            new MovementQuery { Page = 1, PageSize = 2 },
            default);
        var second = await _service.GetMovementsAsync(
            WorkspaceId,
            new MovementQuery { Page = 2, PageSize = 2 },
            default);

        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.DoesNotContain(
            first.Items.Concat(second.Items),
            movement => movement.Sku == "MOVEMENT-OTHER");
    }

    [Fact]
    public async Task Queries_AreScopedToWorkspace()
    {
        await _service.CreateAsync(Request(sku: "PRIMARY-1"), Actor, default);
        var otherActor = new StockActor(OtherWorkspaceId, Guid.NewGuid(), "Other Operator");
        await _service.CreateAsync(Request(sku: "OTHER-1"), otherActor, default);

        var primary = await _service.SearchAsync(WorkspaceId, new InventoryQuery(), default);
        var other = await _service.SearchAsync(OtherWorkspaceId, new InventoryQuery(), default);

        Assert.Single(primary.Items);
        Assert.Equal("PRIMARY-1", primary.Items[0].Sku);
        Assert.Single(other.Items);
        Assert.Equal("OTHER-1", other.Items[0].Sku);
    }

    [Fact]
    public async Task AdjustStock_RejectsNegativeResult()
    {
        var item = await _service.CreateAsync(Request(quantity: 2), Actor, default);

        await Assert.ThrowsAsync<InvalidStockAdjustmentException>(() =>
            _service.AdjustStockAsync(
                item.Id,
                Adjustment(-3, MovementType.Issue, item.Version),
                Actor,
                default));
    }

    [Fact]
    public async Task RejectedAdjustment_LeavesQuantityMovementAndAuditUnchanged()
    {
        var item = await _service.CreateAsync(Request(quantity: 2), Actor, default);
        var movementCount = await _db.InventoryMovements.CountAsync();
        var auditCount = await _db.AuditEvents.CountAsync();

        await Assert.ThrowsAsync<InvalidStockAdjustmentException>(() =>
            _service.AdjustStockAsync(
                item.Id,
                Adjustment(-3, MovementType.Issue, item.Version),
                Actor,
                default));

        _db.ChangeTracker.Clear();
        var stored = await _db.InventoryItems.SingleAsync(candidate => candidate.Id == item.Id);
        Assert.Equal(2, stored.Quantity);
        Assert.Equal(item.Version, stored.Version);
        Assert.Equal(movementCount, await _db.InventoryMovements.CountAsync());
        Assert.Equal(auditCount, await _db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Receipt_TransitionsOrderedItemToAutomaticStockStatus()
    {
        var item = await _service.CreateAsync(
            Request(quantity: 0, procurement: ProcurementStatus.Ordered),
            Actor,
            default);

        var adjusted = await _service.AdjustStockAsync(
            item.Id,
            Adjustment(10, MovementType.Receipt, item.Version),
            Actor,
            default);

        Assert.NotNull(adjusted);
        Assert.Equal(StockStatus.InStock, adjusted.Status);
        Assert.Equal(ProcurementStatus.None, adjusted.ProcurementStatus);
    }

    [Fact]
    public async Task Adjustment_RecordsBalancesActorAuditAndReason()
    {
        var item = await _service.CreateAsync(Request(quantity: 10), Actor, default);
        var requestId = Guid.NewGuid();

        await _service.AdjustStockAsync(
            item.Id,
            Adjustment(-2, MovementType.Damage, item.Version, "Damaged in transit", requestId),
            Actor,
            default);
        var movements = await _service.GetMovementsAsync(
            WorkspaceId,
            new MovementQuery { ItemId = item.Id, PageSize = 10 },
            default);

        Assert.Equal(2, movements.Total);
        Assert.Equal(requestId, movements.Items[0].RequestId);
        Assert.Equal(MovementType.Damage, movements.Items[0].Type);
        Assert.Equal("Damaged in transit", movements.Items[0].Reason);
        Assert.Equal("Test Operator", movements.Items[0].PerformedByName);
        Assert.Equal(10, movements.Items[0].PreviousQuantity);
        Assert.Equal(8, movements.Items[0].NewQuantity);
        Assert.Contains(
            await _db.AuditEvents.ToListAsync(),
            audit => audit.Action == "StockAdjusted" && audit.EntityId == item.Id);
    }

    [Fact]
    public async Task Adjustment_IsIdempotentForSameRequest()
    {
        var item = await _service.CreateAsync(Request(quantity: 10), Actor, default);
        var request = Adjustment(-2, MovementType.Issue, item.Version);

        var first = await _service.AdjustStockAsync(item.Id, request, Actor, default);
        var second = await _service.AdjustStockAsync(item.Id, request, Actor, default);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(8, first.Quantity);
        Assert.Equal(8, second.Quantity);
        Assert.Equal(
            1,
            await _db.InventoryMovements.CountAsync(movement => movement.RequestId == request.RequestId));
    }

    [Fact]
    public async Task Adjustment_RejectsReusedRequestIdWithDifferentPayload()
    {
        var item = await _service.CreateAsync(Request(quantity: 10), Actor, default);
        var requestId = Guid.NewGuid();
        await _service.AdjustStockAsync(
            item.Id,
            Adjustment(-2, MovementType.Issue, item.Version, requestId: requestId),
            Actor,
            default);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            _service.AdjustStockAsync(
                item.Id,
                Adjustment(-3, MovementType.Issue, item.Version + 1, requestId: requestId),
                Actor,
                default));
    }

    [Fact]
    public async Task Update_RejectsDirectQuantityChange()
    {
        var item = await _service.CreateAsync(Request(quantity: 10), Actor, default);
        var changed = Request(quantity: 11, version: item.Version);

        await Assert.ThrowsAsync<QuantityRequiresAdjustmentException>(() =>
            _service.UpdateAsync(item.Id, changed, Actor, default));
    }

    [Fact]
    public async Task StaleVersion_IsRejected()
    {
        var item = await _service.CreateAsync(Request(quantity: 10), Actor, default);
        await _service.AdjustStockAsync(
            item.Id,
            Adjustment(2, MovementType.Receipt, item.Version),
            Actor,
            default);

        await Assert.ThrowsAsync<InventoryConcurrencyException>(() =>
            _service.AdjustStockAsync(
                item.Id,
                Adjustment(1, MovementType.Receipt, item.Version),
                Actor,
                default));
    }

    [Fact]
    public async Task Delete_RemovesItemFromActiveInventoryButRetainsRecordAndHistory()
    {
        var item = await _service.CreateAsync(Request(), Actor, default);

        Assert.True(await _service.DeleteAsync(item.Id, Actor, default));
        Assert.Null(await _service.GetAsync(WorkspaceId, item.Id, default));
        Assert.NotNull(await _db.InventoryItems
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.Id == item.Id));
        var movements = await _service.GetMovementsAsync(
            WorkspaceId,
            new MovementQuery { ItemId = item.Id, PageSize = 10 },
            default);
        Assert.NotEmpty(movements.Items);
    }

    [Fact]
    public async Task Restore_ReturnsArchivedItemToActiveInventoryAndAuditsAction()
    {
        var item = await _service.CreateAsync(Request(), Actor, default);
        Assert.True(await _service.DeleteAsync(item.Id, Actor, default));

        var archived = await _service.SearchArchivedAsync(
            WorkspaceId,
            new InventoryQuery(),
            default);
        var archivedItem = Assert.Single(archived.Items);
        Assert.True(archivedItem.IsArchived);
        Assert.NotNull(archivedItem.DeletedAtUtc);

        var restored = await _service.RestoreAsync(item.Id, Actor, default);
        var retried = await _service.RestoreAsync(item.Id, Actor, default);

        Assert.NotNull(restored);
        Assert.NotNull(retried);
        Assert.Equal(restored.Version, retried.Version);
        Assert.False(restored.IsArchived);
        Assert.Null(restored.DeletedAtUtc);
        Assert.NotNull(await _service.GetAsync(WorkspaceId, item.Id, default));
        Assert.Single(await _db.AuditEvents.Where(
            audit => audit.Action == "Restored" && audit.EntityId == item.Id).ToListAsync());
    }

    [Fact]
    public async Task Restore_RejectsSkuThatWasReusedWhileArchived()
    {
        var archived = await _service.CreateAsync(Request(sku: "REUSED-1"), Actor, default);
        Assert.True(await _service.DeleteAsync(archived.Id, Actor, default));
        await _service.CreateAsync(Request(sku: "REUSED-1"), Actor, default);

        await Assert.ThrowsAsync<DuplicateSkuException>(() =>
            _service.RestoreAsync(archived.Id, Actor, default));
    }

    private static SaveInventoryItemRequest Request(
        string name = "Test item",
        string sku = "TEST-1",
        string category = "Test",
        int quantity = 10,
        int reorder = 5,
        string location = "",
        string supplier = "",
        ProcurementStatus procurement = ProcurementStatus.None,
        InventoryLifecycleStatus lifecycle = InventoryLifecycleStatus.Active,
        long? version = null) => new()
    {
        Name = name,
        Sku = sku,
        Category = category,
        Location = location,
        Supplier = supplier,
        Quantity = quantity,
        ReorderLevel = reorder,
        PurchasePrice = 10,
        SellingPrice = 15,
        ProcurementStatus = procurement,
        LifecycleStatus = lifecycle,
        Version = version
    };

    private static StockAdjustmentRequest Adjustment(
        int change,
        MovementType type,
        long version,
        string reason = "Test adjustment",
        Guid? requestId = null) => new()
    {
        RequestId = requestId ?? Guid.NewGuid(),
        Change = change,
        Type = type,
        Reason = reason,
        Version = version
    };

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
