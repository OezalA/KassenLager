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
}
