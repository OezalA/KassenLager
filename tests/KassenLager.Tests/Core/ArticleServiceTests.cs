using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class ArticleServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ArticleService _service;

    public ArticleServiceTests() => _service = new ArticleService(_database.Factory);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task SaveAsync_SerialCategoryWithoutModel_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.SaveAsync(null, Input(categoryId: TestDatabase.SerialCategoryId, model: "  ")));

        Assert.Equal(Messages.ArticleModelRequired, ex.Message);
    }

    [Fact]
    public async Task SaveAsync_QuantityCategoryWithoutModel_Succeeds()
    {
        var id = await _service.SaveAsync(null, Input(categoryId: TestDatabase.QuantityCategoryId, model: null));

        var article = await _service.GetAsync(id);
        Assert.Null(article.Model);
        Assert.Equal("Kabel", article.Category!.Name);
        Assert.Equal("Stück", article.Unit!.Name);
    }

    [Fact]
    public async Task SaveAsync_DuplicateArticleNumberIgnoringCaseAndSpaces_Throws()
    {
        await _service.SaveAsync(null, Input(articleNumber: "AB-100"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.SaveAsync(null, Input(articleNumber: " ab-100 ", name: "Anderer Artikel")));
        Assert.Contains("AB-100", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_SeveralArticlesWithoutNumber_Succeed()
    {
        await _service.SaveAsync(null, Input(articleNumber: null, name: "A"));
        await _service.SaveAsync(null, Input(articleNumber: "", name: "B"));

        Assert.Equal(2, (await _service.GetListAsync()).Count);
    }

    [Fact]
    public async Task SaveAsync_UpdateKeepingOwnArticleNumber_Succeeds()
    {
        var id = await _service.SaveAsync(null, Input(articleNumber: "AB-100"));

        await _service.SaveAsync(id, Input(articleNumber: "ab-100", name: "Umbenannt"));

        var article = await _service.GetAsync(id);
        Assert.Equal("Umbenannt", article.Name);
        Assert.Equal("ab-100", article.ArticleNumber);
    }

    [Fact]
    public async Task SaveAsync_WithoutCategory_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SaveAsync(null, Input() with { CategoryId = null }));

        Assert.Equal(Messages.ArticleCategoryRequired, ex.Message);
    }

    [Fact]
    public async Task SaveAsync_UnknownUnit_Throws()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.SaveAsync(null, Input() with { UnitId = 999 }));
    }

    [Fact]
    public async Task FindDuplicateModelAsync_SameManufacturerAndModelIgnoringCase_ReturnsExisting()
    {
        var id = await _service.SaveAsync(null, Input(manufacturer: "Diebold Nixdorf", model: "BEETLE /M-II plus"));

        var duplicate = await _service.FindDuplicateModelAsync(" diebold nixdorf ", "beetle /m-ii PLUS", excludeId: null);

        Assert.Equal(id, duplicate?.Id);
    }

    [Fact]
    public async Task FindDuplicateModelAsync_ExcludesTheEditedArticle()
    {
        var id = await _service.SaveAsync(null, Input(manufacturer: "Zebra", model: "DS2208"));

        Assert.Null(await _service.FindDuplicateModelAsync("Zebra", "DS2208", excludeId: id));
    }

    [Fact]
    public async Task FindDuplicateModelAsync_DifferentManufacturer_ReturnsNull()
    {
        await _service.SaveAsync(null, Input(manufacturer: "Zebra", model: "DS2208"));

        Assert.Null(await _service.FindDuplicateModelAsync("Honeywell", "DS2208", excludeId: null));
    }

    [Fact]
    public async Task GetListAsync_FiltersByCategoryAndActiveState()
    {
        await _service.SaveAsync(null, Input(name: "Rechner", categoryId: TestDatabase.SerialCategoryId, model: "X1"));
        await _service.SaveAsync(null, Input(name: "Kabel alt", categoryId: TestDatabase.QuantityCategoryId) with { IsActive = false });

        Assert.Single(await _service.GetListAsync(categoryId: TestDatabase.SerialCategoryId));
        Assert.Single(await _service.GetListAsync(includeInactive: false));
        Assert.Equal(2, (await _service.GetListAsync()).Count);
    }

    [Theory]
    [InlineData("ds22", true)]
    [InlineData("ZEBRA", true)]
    [InlineData("4711", true)]
    [InlineData("handscanner", true)]
    [InlineData("honeywell", false)]
    [InlineData("", true)]
    public void ArticleListItem_Matches_SearchesIdentifyingFields(string text, bool expected)
    {
        var item = new ArticleListItem(1, "4711", "Handscanner", "Zebra", "DS2208", null, 11, "Handscanner", TrackingType.Serial, "Stück", true);

        Assert.Equal(expected, item.Matches(text));
    }

    private static ArticleInput Input(
        string? articleNumber = null,
        string name = "Testartikel",
        int categoryId = TestDatabase.QuantityCategoryId,
        string? manufacturer = null,
        string? model = "M1") =>
        new(articleNumber, name, categoryId, manufacturer, model, null, TestDatabase.PieceUnitId, null, true);
}
