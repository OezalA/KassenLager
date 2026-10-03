using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class CategoryAndUnitServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly CategoryService _categories;
    private readonly UnitService _units;
    private readonly ArticleService _articles;

    public CategoryAndUnitServiceTests()
    {
        _categories = new CategoryService(_database.Factory);
        _units = new UnitService(_database.Factory);
        _articles = new ArticleService(_database.Factory);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Category_ChangeTrackingType_WithoutArticles_Succeeds()
    {
        await _categories.SaveAsync(TestDatabase.QuantityCategoryId, new CategoryInput("Kabel", TrackingType.Serial, 130, true));

        var category = await _categories.GetAsync(TestDatabase.QuantityCategoryId);
        Assert.Equal(TrackingType.Serial, category.TrackingType);
    }

    [Fact]
    public async Task Category_ChangeTrackingType_WithArticles_Throws()
    {
        await AddArticleAsync(TestDatabase.QuantityCategoryId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _categories.SaveAsync(TestDatabase.QuantityCategoryId, new CategoryInput("Kabel", TrackingType.Serial, 130, true)));
        Assert.Contains("Erfassungsart", ex.Message);
    }

    [Fact]
    public async Task Category_RenameWithArticles_Succeeds()
    {
        await AddArticleAsync(TestDatabase.QuantityCategoryId);

        await _categories.SaveAsync(TestDatabase.QuantityCategoryId, new CategoryInput("Kabel & Adapter", TrackingType.Quantity, 130, true));

        Assert.Equal("Kabel & Adapter", (await _categories.GetAsync(TestDatabase.QuantityCategoryId)).Name);
    }

    [Fact]
    public async Task Category_DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _categories.SaveAsync(null, new CategoryInput("hub", TrackingType.Serial, 0, true)));
    }

    [Fact]
    public async Task Category_DeleteWithArticles_Throws()
    {
        await AddArticleAsync(TestDatabase.QuantityCategoryId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _categories.DeleteAsync(TestDatabase.QuantityCategoryId));
    }

    [Fact]
    public async Task Category_DeleteUnused_Removes()
    {
        await _categories.DeleteAsync(TestDatabase.QuantityCategoryId);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _categories.GetAsync(TestDatabase.QuantityCategoryId));
    }

    [Fact]
    public async Task Category_GetAll_OrdersBySortOrderAndCountsArticles()
    {
        await AddArticleAsync(TestDatabase.QuantityCategoryId);

        var all = await _categories.GetAllAsync();

        Assert.Equal("Kassenrechner", all[0].Name);
        Assert.Equal("Sonstiges", all[^1].Name);
        Assert.Equal(1, all.Single(c => c.Id == TestDatabase.QuantityCategoryId).ArticleCount);
    }

    [Fact]
    public async Task Unit_DeleteUsed_Throws()
    {
        await AddArticleAsync(TestDatabase.QuantityCategoryId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _units.DeleteAsync(TestDatabase.PieceUnitId));
    }

    [Fact]
    public async Task Unit_DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _units.SaveAsync(null, "STÜCK"));
    }

    [Fact]
    public async Task Unit_AddRenameDelete_Works()
    {
        var id = await _units.SaveAsync(null, "Karton");
        await _units.SaveAsync(id, "Kiste");
        Assert.Contains(await _units.GetAllAsync(), u => u.Id == id && u.Name == "Kiste");

        await _units.DeleteAsync(id);
        Assert.DoesNotContain(await _units.GetAllAsync(), u => u.Id == id);
    }

    private Task<int> AddArticleAsync(int categoryId) =>
        _articles.SaveAsync(null, new ArticleInput(null, "Testartikel", categoryId, null, "M1", null, TestDatabase.PieceUnitId, null, true));
}
