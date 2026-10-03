using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class StockServiceTests : LedgerTestBase
{
    [Fact]
    public async Task Lines_CountAvailableAndDefectiveDevicesPerCustomer()
    {
        var pc = await CreateSerialArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer1, pc, "SN-2", DeviceState.UsedWorking);
        await ReceiveDeviceAsync(Customer1, pc, "SN-3", DeviceState.Defective);
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "SN-4"));
        await ReceiveDeviceAsync(Customer2, pc, "SN-5");

        var line = Assert.Single(await Stock.GetLinesAsync(Customer1));

        Assert.Equal(2, line.Available);
        Assert.Equal(1, line.Defective);
        Assert.Equal(3, line.Quantity);
        Assert.Equal("2 verfügbar · 1 defekt", line.StockText);
    }

    [Fact]
    public async Task Minimum_SerialArticle_IsComparedWithAvailableDevicesOnly()
    {
        var pc = await CreateSerialArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer1, pc, "SN-2", DeviceState.Defective);

        await Stock.SetMinimumAsync(pc, Customer1, 2);

        var line = Assert.Single(await Stock.GetLinesAsync(Customer1));
        Assert.Equal(2, line.Minimum);
        Assert.True(line.IsBelowMinimum);
    }

    [Fact]
    public async Task Minimum_QuantityArticle_WithoutStock_StillShowsUpBelowMinimum()
    {
        var cable = await CreateQuantityArticleAsync();

        await Stock.SetMinimumAsync(cable, Customer2, 3);

        var line = Assert.Single(await Stock.GetLinesAsync(Customer2));
        Assert.Equal(0, line.Quantity);
        Assert.True(line.IsBelowMinimum);
        Assert.Empty(await Stock.GetLinesAsync(Customer1));
    }

    [Fact]
    public async Task Minimum_ReachedExactly_IsNotBelow()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 3);

        await Stock.SetMinimumAsync(cable, Customer1, 3);

        Assert.False(Assert.Single(await Stock.GetLinesAsync(Customer1)).IsBelowMinimum);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Minimum_NullOrZero_RemovesIt(int? minimum)
    {
        var cable = await CreateQuantityArticleAsync();
        await Stock.SetMinimumAsync(cable, Customer1, 5);

        await Stock.SetMinimumAsync(cable, Customer1, minimum);

        Assert.Empty(await Stock.GetLinesAsync(Customer1));
    }

    [Fact]
    public async Task Minimum_Negative_Throws()
    {
        var cable = await CreateQuantityArticleAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Stock.SetMinimumAsync(cable, Customer1, -1));
    }

    [Fact]
    public async Task ArticleLines_ContainEveryActiveCustomer()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer3, cable, 7);

        var lines = await Stock.GetArticleLinesAsync(cable);

        Assert.Equal(["Kunde 1", "Kunde 2", "Kunde 3", "Kunde 4"], lines.Select(l => l.CustomerName));
        Assert.Equal(7, lines.Single(l => l.CustomerId == Customer3).Quantity);
    }

    [Fact]
    public async Task CustomerSummaries_AggregateDevicesQuantitiesAndMinimums()
    {
        var pc = await CreateSerialArticleAsync();
        var cable = await CreateQuantityArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer1, pc, "SN-2", DeviceState.Defective);
        await ReceiveQuantityAsync(Customer1, cable, 4);
        await Stock.SetMinimumAsync(cable, Customer1, 10);

        var summary = (await Stock.GetCustomerSummariesAsync()).Single(s => s.CustomerId == Customer1);

        Assert.Equal(1, summary.AvailableDevices);
        Assert.Equal(1, summary.DefectiveDevices);
        Assert.Equal(1, summary.QuantityArticles);
        Assert.Equal(1, summary.BelowMinimum);
    }

    [Fact]
    public async Task DeleteArticle_WithHistory_Throws_WithoutHistory_RemovesMinimumToo()
    {
        var used = await CreateQuantityArticleAsync("Benutzt");
        var unused = await CreateQuantityArticleAsync("Unbenutzt");
        await ReceiveQuantityAsync(Customer1, used, 1);
        await Stock.SetMinimumAsync(unused, Customer1, 2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Articles.DeleteAsync(used));
        Assert.Equal(Messages.ArticleHasHistory, ex.Message);

        await Articles.DeleteAsync(unused);
        Assert.DoesNotContain(await Stock.GetLinesAsync(), l => l.ArticleId == unused);
    }

    [Fact]
    public async Task DeleteCustomer_WithHistory_Throws()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => new CustomerService(Database.Factory).DeleteAsync(Customer1));
        Assert.Equal(Messages.CustomerHasHistory, ex.Message);
    }

    private const int Customer3 = 3;
}
