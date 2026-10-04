using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class ExportServiceTests : LedgerTestBase
{
    private ExportService Export => new(Database.Factory, Stock, Clock);

    [Fact]
    public async Task Template_HasImportSheetsAndValidValues_AndCanBeReadBack()
    {
        using var stream = new MemoryStream();
        await Export.WriteTemplateAsync(stream);

        Assert.Equal(["Artikel", "Bestand", "Geräte", "Listen"], Xlsx.SheetNames(stream));
        Assert.Equal(ExcelWorkbook.ArticleHeaders, Xlsx.Rows(stream, "Artikel")[0]);
        var lists = Xlsx.Rows(stream, "Listen");
        Assert.Contains(lists, r => r[0] == "Kassenrechner" && r[1] == "Seriennummer");
        Assert.Contains(lists, r => r[2] == "Kunde 1");
        Assert.Contains(lists, r => r[3] == "Gebraucht – funktionsfähig");

        stream.Position = 0;
        var workbook = ExcelWorkbook.Read(stream);
        Assert.Equal(0, workbook.RowCount);
    }

    [Fact]
    public async Task Stock_OneSheetPerCustomerAndADeviceSheet()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        var cable = await CreateQuantityArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "S1");
        await ReceiveDeviceAsync(Customer1, pc, "S2", DeviceState.Defective);
        await ReceiveQuantityAsync(Customer2, cable, 4);
        await Stock.SetMinimumAsync(cable, Customer2, 10);

        using var stream = new MemoryStream();
        await Export.WriteStockAsync(stream);

        Assert.Equal(["Kunde 1", "Kunde 2", "Kunde 3", "Kunde 4", "Geräte"], Xlsx.SheetNames(stream));
        var customer1 = Xlsx.Rows(stream, "Kunde 1");
        Assert.Equal("Kassenrechner||Kassenrechner|Diebold Nixdorf|X1|Stück|2|1|1||", string.Join("|", customer1[1]));
        var customer2 = Xlsx.Rows(stream, "Kunde 2");
        Assert.Equal("Ja", customer2[1][10]);
        var devices = Xlsx.Rows(stream, "Geräte");
        Assert.Equal(3, devices.Count);
        Assert.Equal("Kunde 1|Diebold Nixdorf|X1|S2|Defekt|Kassenrechner", string.Join("|", devices[2]));
    }

    [Fact]
    public async Task Journal_ContainsOnlyTheDateRangeAndCustomer()
    {
        var cable = await CreateQuantityArticleAsync();
        await Bookings.ReceiveQuantityAsync(new QuantityReceiptInput(Customer1, cable, 1, null, null, Clock.Today.AddDays(-10)));
        await ReceiveQuantityAsync(Customer1, cable, 2);
        await ReceiveQuantityAsync(Customer2, cable, 3);

        using var stream = new MemoryStream();
        await Export.WriteJournalAsync(stream, Clock.Today.AddDays(-1), Clock.Today, Customer1);

        var rows = Xlsx.Rows(stream, "Bewegungsjournal");
        Assert.Equal(2, rows.Count);
        Assert.Equal("03.10.2026", rows[1][1]);
        Assert.Equal("Wareneingang", rows[1][3]);
        Assert.Equal("2", rows[1][9]);
    }

    [Fact]
    public async Task BranchIssues_ListsIssuesWithReturnDate()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "S1"), "Filiale 7", "KD-1");
        await Branches.ReturnAsync(new BranchReturnInput(Customer1, "S1", null, null, "Filiale 7", DeviceState.Defective, null, null, null));

        using var stream = new MemoryStream();
        await Export.WriteBranchIssuesAsync(stream, Clock.Today, Clock.Today, null);

        var row = Xlsx.Rows(stream, "Ausgaben")[1];
        Assert.Equal("03.10.2026|Kunde 1|Filiale 7|Leihgerät", string.Join("|", row[..4]));
        Assert.Equal("KD-1", row[8]);
        Assert.Equal("03.10.2026", row[13]);
    }

    [Fact]
    public async Task DateRange_EndBeforeStart_Throws()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Export.WriteJournalAsync(stream, Clock.Today, Clock.Today.AddDays(-1), null));
    }

    [Theory]
    [InlineData("Markt [Nord]: Lager?", "Markt -Nord-- Lager-")]
    [InlineData("Eine sehr lange Kundenbezeichnung mit vielen Wörtern", "Eine sehr lange Kundenbezeichnu")]
    public void SheetNames_AreMadeValidForExcel(string name, string expected)
    {
        Assert.Equal(expected, ExcelWriter.UniqueName(name, []));
    }

    [Fact]
    public void SheetNames_AreMadeUnique()
    {
        Assert.Equal("Geräte (2)", ExcelWriter.UniqueName("Geräte", ["geräte"]));
    }
}
