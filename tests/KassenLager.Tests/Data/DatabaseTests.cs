using KassenLager.Core.Domain;
using KassenLager.Data;
using KassenLager.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Tests.Data;

public sealed class DatabaseTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Migrations_AreInSyncWithModel()
    {
        using var db = _database.CreateContext();

        Assert.False(db.Database.HasPendingModelChanges(), "Model changed without a migration: run 'dotnet ef migrations add'.");
        Assert.Empty(db.Database.GetPendingMigrations());
    }

    [Fact]
    public async Task Migrations_SeedCustomersCategoriesAndUnits()
    {
        await using var db = _database.CreateContext();

        var customers = await db.Customers.OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();
        Assert.Equal(new[] { "Kunde 1", "Kunde 2", "Kunde 3", "Kunde 4" }, customers);

        var categories = await db.Categories.ToListAsync();
        Assert.Equal(16, categories.Count);
        Assert.Equal(12, categories.Count(c => c.TrackingType == TrackingType.Serial));
        Assert.Contains(categories, c => c.Name == "Verbrauchs- und Verschleißmaterial" && c.TrackingType == TrackingType.Quantity);

        var units = await db.Units.Select(u => u.Name).ToListAsync();
        Assert.Equivalent(new[] { "Stück", "Rolle", "Packung" }, units);
    }

    [Fact]
    public async Task SaveChanges_FillsNormalizedArticleNumberKey()
    {
        await using (var db = _database.CreateContext())
        {
            db.Articles.Add(new Article
            {
                ArticleNumber = " ab-123 ",
                Name = "Netzteil",
                CategoryId = TestDatabase.QuantityCategoryId,
                UnitId = TestDatabase.PieceUnitId,
            });
            await db.SaveChangesAsync();
        }

        await using var check = _database.CreateContext();
        var article = await check.Articles.SingleAsync();
        Assert.Equal("AB-123", article.ArticleNumberKey);
    }

    [Fact]
    public async Task DeviceSerialNumber_IsUniquePerArticleAmongNonVoidedDevices()
    {
        var articleId = await AddArticleAsync(TestDatabase.SerialCategoryId);
        await using (var db = _database.CreateContext())
        {
            db.Devices.Add(NewDevice(articleId, "sn-1", isVoided: true));
            db.Devices.Add(NewDevice(articleId, "SN-1", isVoided: false));
            await db.SaveChangesAsync();
        }

        await using var duplicate = _database.CreateContext();
        duplicate.Devices.Add(NewDevice(articleId, " Sn-1 ", isVoided: false));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task Movement_CanBeReversedOnlyOnceAtDatabaseLevel()
    {
        var articleId = await AddArticleAsync(TestDatabase.QuantityCategoryId);
        int originalId;
        await using (var db = _database.CreateContext())
        {
            var original = NewMovement(articleId, MovementType.GoodsReceipt, 1, null);
            db.Movements.Add(original);
            await db.SaveChangesAsync();
            originalId = original.Id;
            db.Movements.Add(NewMovement(articleId, MovementType.Reversal, -1, originalId));
            await db.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        second.Movements.Add(NewMovement(articleId, MovementType.Reversal, -1, originalId));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DateTimes_AreReadBackAsUtc()
    {
        var articleId = await AddArticleAsync(TestDatabase.QuantityCategoryId);
        await using (var db = _database.CreateContext())
        {
            db.Movements.Add(NewMovement(articleId, MovementType.GoodsReceipt, 1, null));
            await db.SaveChangesAsync();
        }

        await using var check = _database.CreateContext();
        var movement = await check.Movements.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, movement.OccurredAt.Kind);
        Assert.Equal(new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc), movement.OccurredAt);
    }

    [Fact]
    public async Task CategoryWithArticles_CannotBeDeletedAtDatabaseLevel()
    {
        await using (var setup = _database.CreateContext())
        {
            setup.Articles.Add(new Article { Name = "Kabel USB", CategoryId = TestDatabase.QuantityCategoryId, UnitId = TestDatabase.PieceUnitId });
            await setup.SaveChangesAsync();
        }

        // Fresh context: the article is not tracked, so the foreign key itself must refuse the delete.
        await using var db = _database.CreateContext();
        db.Categories.Remove(await db.Categories.SingleAsync(c => c.Id == TestDatabase.QuantityCategoryId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static readonly DateTime Now = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    private async Task<int> AddArticleAsync(int categoryId)
    {
        await using var db = _database.CreateContext();
        var article = new Article { Name = "Test", Model = "M1", CategoryId = categoryId, UnitId = TestDatabase.PieceUnitId };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    private static Device NewDevice(int articleId, string serialNumber, bool isVoided) => new()
    {
        ArticleId = articleId,
        CustomerId = 1,
        SerialNumber = serialNumber,
        State = DeviceState.New,
        IsVoided = isVoided,
        CreatedAt = Now,
        StateChangedAt = Now,
    };

    private static Movement NewMovement(int articleId, MovementType type, int quantityChange, int? reversalOfId) => new()
    {
        Type = type,
        OccurredAt = Now,
        RecordedAt = Now,
        CustomerId = 1,
        ArticleId = articleId,
        QuantityChange = quantityChange,
        ReversalOfId = reversalOfId,
    };
}
