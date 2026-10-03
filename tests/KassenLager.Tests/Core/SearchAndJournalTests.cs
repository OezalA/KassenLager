using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class SearchAndJournalTests : LedgerTestBase
{
    [Fact]
    public async Task Search_ByPartialModelIgnoringCase_ReturnsStockPerCustomer()
    {
        var pc = await CreateSerialArticleAsync(model: "BEETLE /M-III");
        await CreateSerialArticleAsync("Bondrucker", "TM-T88VI", "Epson");
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer1, pc, "SN-2", DeviceState.Defective);
        await ReceiveDeviceAsync(Customer2, pc, "SN-3");

        var result = await Search.SearchAsync("beetle /m-iii");

        var hit = Assert.Single(result.Articles);
        Assert.Equal(pc, hit.Article.Id);
        Assert.Equal(2, hit.Stock.Count);
        var customer1 = hit.Stock.Single(s => s.CustomerId == Customer1);
        Assert.Equal(1, customer1.Available);
        Assert.Equal(1, customer1.Defective);
    }

    [Fact]
    public async Task Search_EveryWordMustMatchSomeField()
    {
        await CreateSerialArticleAsync("Bondrucker", "TM-T88VI", "Epson");
        await CreateSerialArticleAsync("Bondrucker", "TM-T20", "Epson");

        var result = await Search.SearchAsync("epson t88");

        Assert.Equal("TM-T88VI", Assert.Single(result.Articles).Article.Model);
    }

    [Fact]
    public async Task Search_CustomerFilter_ShowsOnlyThatCustomersStock()
    {
        var pc = await CreateSerialArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer2, pc, "SN-2");

        var result = await Search.SearchAsync("beetle", Customer2);

        var line = Assert.Single(Assert.Single(result.Articles).Stock);
        Assert.Equal(Customer2, line.CustomerId);
        Assert.Equal("SN-2", Assert.Single((await Search.SearchAsync("sn-", Customer2)).Devices).SerialNumber);
    }

    [Fact]
    public async Task Search_ArticleWithoutStock_IsListedAfterArticlesWithStock()
    {
        var withStock = await CreateQuantityArticleAsync("Kabel B");
        await CreateQuantityArticleAsync("Kabel A");
        await ReceiveQuantityAsync(Customer1, withStock, 2);

        var result = await Search.SearchAsync("kabel");

        Assert.Equal(["Kabel B", "Kabel A"], result.Articles.Select(h => h.Article.Name));
        Assert.False(result.Articles[1].HasStock);
    }

    [Fact]
    public async Task Search_ExactSerialNumber_ReturnsTheDevice()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "ABC-123");
        await ReceiveDeviceAsync(Customer1, pc, "ABC-1234");

        var exact = await Search.SearchAsync(" abc-123 ");
        var partial = await Search.SearchAsync("abc-12");

        Assert.Equal(deviceId, exact.ExactDevice?.Id);
        Assert.Equal(2, exact.Devices.Count);
        Assert.Null(partial.ExactDevice);
        Assert.Equal(2, partial.Devices.Count);
    }

    [Fact]
    public async Task Search_FindsIssuedDevicesButNotVoidedOnes()
    {
        var pc = await CreateSerialArticleAsync();
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "ISSUED-1"));
        await ReceiveDeviceAsync(Customer1, pc, "VOID-1");
        await Reversals.ReverseAsync(await LatestMovementIdAsync(), null);

        Assert.Equal(DeviceState.Issued, Assert.Single((await Search.SearchAsync("issued")).Devices).State);
        Assert.Empty((await Search.SearchAsync("void")).Devices);
    }

    [Fact]
    public async Task Search_EmptyText_ReturnsNothing()
    {
        await CreateQuantityArticleAsync();

        Assert.True((await Search.SearchAsync("   ")).IsEmpty);
    }

    [Fact]
    public async Task FindBySerialNumber_IgnoresCaseAndCanBeNarrowedToAnArticle()
    {
        var pc = await CreateSerialArticleAsync();
        var printer = await CreateSerialArticleAsync("Bondrucker", "TM-T88VI", "Epson");
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await ReceiveDeviceAsync(Customer1, printer, "SN-1");

        Assert.Equal(2, (await Devices.FindBySerialNumberAsync("sn-1")).Count);
        Assert.Equal(printer, Assert.Single(await Devices.FindBySerialNumberAsync("SN-1", printer)).ArticleId);
        Assert.Empty(await Devices.FindBySerialNumberAsync("SN"));
    }

    [Fact]
    public async Task DeviceNote_CanBeEdited()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Devices.SetNoteAsync(deviceId, "  Kratzer am Gehäuse ");

        Assert.Equal("Kratzer am Gehäuse", (await Devices.GetDetailAsync(deviceId)).Note);
    }

    [Fact]
    public async Task Journal_IsNewestFirstAndFilterable()
    {
        var cable = await CreateQuantityArticleAsync();
        var first = await ReceiveQuantityAsync(Customer1, cable, 5);
        Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await ReceiveQuantityAsync(Customer2, cable, 1);
        Clock.Advance(TimeSpan.FromMinutes(5));
        var third = await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 1, null, null, null, null));

        Assert.Equal([third, second, first], (await Journal.GetPageAsync(new MovementFilter(), 0, 10)).Select(m => m.Id));
        Assert.Equal([third, first], (await Journal.GetPageAsync(new MovementFilter(CustomerId: Customer1), 0, 10)).Select(m => m.Id));
        Assert.Equal([third], (await Journal.GetPageAsync(new MovementFilter(Type: MovementType.Consumption), 0, 10)).Select(m => m.Id));
        Assert.Equal([second], (await Journal.GetPageAsync(new MovementFilter(), 1, 1)).Select(m => m.Id));
    }

    [Fact]
    public async Task MovementListItem_DescribesStateTransitions()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await IssueAsync(Customer1, deviceId, "Filiale Nord");

        var history = (await Devices.GetDetailAsync(deviceId)).History;

        Assert.Equal("Neu → Ausgegeben", history[0].StateText);
        Assert.Equal("Kunde 1 · Filiale Nord", history[0].DetailText);
        Assert.Equal("−1 Stück", history[0].QuantityText);
        Assert.Equal("neu erfasst: Neu", history[1].StateText);
    }
}
