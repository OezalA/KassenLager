using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;
using static KassenLager.Tests.Infrastructure.Xlsx;

namespace KassenLager.Tests.Core;

public sealed class ImportServiceTests : LedgerTestBase
{
    private static readonly string[] ArticleHeaders = ExcelWorkbook.ArticleHeaders;
    private static readonly string[] StockHeaders = ExcelWorkbook.StockHeaders;
    private static readonly string[] DeviceHeaders = ExcelWorkbook.DeviceHeaders;

    private ImportService Import => new(Database.Factory, Clock);

    [Fact]
    public async Task Preview_ShowsNewArticlesButChangesNothing()
    {
        var workbook = Workbook(Sheet("Artikel", ArticleHeaders,
            ["A-1", "USB-Kabel", "Kabel", null, null, null, "Stück", null]));

        var result = await Import.PreviewAsync(workbook, ImportMode.Set);

        Assert.Equal(ImportRowStatus.New, Assert.Single(result.Rows).Status);
        Assert.Empty(await Articles.GetListAsync());
    }

    [Fact]
    public async Task Import_ArticlesNewUpdatedUnchanged_AndWritesLog()
    {
        await Articles.SaveAsync(null, new ArticleInput("A-1", "USB-Kabel", TestDatabase.QuantityCategoryId, null, null, null, TestDatabase.PieceUnitId, null, true));
        await Articles.SaveAsync(null, new ArticleInput("A-2", "LAN-Kabel", TestDatabase.QuantityCategoryId, null, null, null, TestDatabase.PieceUnitId, null, true));

        var workbook = Workbook(Sheet("Artikel", ArticleHeaders,
            ["a-1", "USB-Kabel", "Kabel", null, null, null, null, null],
            ["A-2", "LAN-Kabel 5 m", null, "Generic", null, null, null, null],
            ["A-3", "Thermoleiste", "Verbrauchs- und Verschleißmaterial", "Epson", "TM-T88", "4006381333931", "Packung", "für Bondrucker"]));

        var result = await Import.ImportAsync(workbook, ImportMode.Set, "Artikel.xlsx");

        Assert.Equal([ImportRowStatus.Unchanged, ImportRowStatus.Updated, ImportRowStatus.New], result.Rows.Select(r => r.Status));
        Assert.Contains("Bezeichnung", result.Rows[1].Message);
        var list = await Articles.GetListAsync();
        Assert.Equal(3, list.Count);
        Assert.Equal("Generic", list.Single(a => a.ArticleNumber == "A-2").Manufacturer);
        Assert.Equal("Packung", list.Single(a => a.ArticleNumber == "A-3").UnitName);

        var log = Assert.Single(await Import.GetLogAsync());
        Assert.Equal("Artikel.xlsx", log.FileName);
        Assert.Equal((1, 1, 1, 0), (log.NewRows, log.UpdatedRows, log.UnchangedRows, log.ErrorRows));
    }

    [Fact]
    public async Task Import_FaultyRowsAreReportedWithReasonAndSkipped()
    {
        var workbook = Workbook(Sheet("Artikel", ArticleHeaders,
            ["A-1", "Unbekannt", "Gibt es nicht", null, null, null, null, null],
            ["A-2", "Kassenrechner", "Kassenrechner", "Diebold", null, null, null, null],
            ["A-3", "Kabel gut", "Kabel", null, null, null, null, null],
            ["a-3", "Kabel doppelt", "Kabel", null, null, null, null, null]));

        var result = await Import.ImportAsync(workbook, ImportMode.Set, "x.xlsx");

        Assert.Equal(3, result.ErrorCount);
        Assert.Contains("Kategorie", result.Rows[0].Message);
        Assert.Equal(Messages.ArticleModelRequired, result.Rows[1].Message);
        Assert.Equal(Messages.Format(Messages.ImportDuplicateRow, 4), result.Rows[3].Message);
        Assert.Equal(5, result.Rows[3].RowNumber);
        Assert.Equal("Kabel gut", Assert.Single(await Articles.GetListAsync()).Name);
    }

    [Fact]
    public async Task Import_WithoutArticleNumber_MatchesByManufacturerAndModel()
    {
        var pc = await CreateSerialArticleAsync(model: "BEETLE /M-III", manufacturer: "Diebold Nixdorf");

        var result = await Import.ImportAsync(Workbook(Sheet("Artikel", ArticleHeaders,
            [null, "Kassen-PC", "Kassenrechner", "diebold nixdorf", "beetle /m-iii", null, null, null])), ImportMode.Set, "x.xlsx");

        Assert.Equal(ImportRowStatus.Updated, Assert.Single(result.Rows).Status);
        Assert.Equal("Kassen-PC", (await Articles.GetAsync(pc)).Name);
    }

    [Fact]
    public async Task StockSet_BooksTheDifferenceAsImportCorrection()
    {
        var cable = await CreateQuantityArticleAsync(articleNumber: "K-1");
        await ReceiveQuantityAsync(Customer1, cable, 5);

        var result = await Import.ImportAsync(Workbook(Sheet("Bestand", StockHeaders,
            ["K-1", "Kunde 1", 3, 4],
            ["K-1", "kunde 2", "1.000", null])), ImportMode.Set, "x.xlsx");

        Assert.Equal([ImportRowStatus.Updated, ImportRowStatus.New], result.Rows.Select(r => r.Status));
        Assert.Equal(3, await Stock.GetQuantityAsync(cable, Customer1));
        Assert.Equal(1000, await Stock.GetQuantityAsync(cable, Customer2));
        var correction = (await Journal.GetPageAsync(new MovementFilter(Customer1, MovementType.ImportCorrection), 0, 10)).Single();
        Assert.Equal(-2, correction.QuantityChange);
        Assert.Equal(4, (await Stock.GetLinesAsync(Customer1)).Single().Minimum);
    }

    [Fact]
    public async Task StockAdd_BooksGoodsReceipt_AndUnchangedRowsBookNothing()
    {
        var cable = await CreateQuantityArticleAsync(articleNumber: "K-1");
        await ReceiveQuantityAsync(Customer1, cable, 5);

        var add = await Import.ImportAsync(Workbook(Sheet("Bestand", StockHeaders, ["K-1", "Kunde 1", 3, null])), ImportMode.Add, "x.xlsx");
        var set = await Import.ImportAsync(Workbook(Sheet("Bestand", StockHeaders, ["K-1", "Kunde 1", 8, null])), ImportMode.Set, "x.xlsx");

        Assert.Equal(ImportRowStatus.Updated, Assert.Single(add.Rows).Status);
        Assert.Equal(ImportRowStatus.Unchanged, Assert.Single(set.Rows).Status);
        Assert.Equal(8, await Stock.GetQuantityAsync(cable, Customer1));
        Assert.Equal(2, (await Journal.GetPageAsync(new MovementFilter(Type: MovementType.GoodsReceipt), 0, 10)).Count);
    }

    [Fact]
    public async Task Stock_InvalidRows_AreErrors()
    {
        await CreateQuantityArticleAsync(articleNumber: "K-1");
        await CreateSerialArticleAsync();
        await Articles.SaveAsync(null, new ArticleInput("PC-1", "PC", TestDatabase.SerialCategoryId, null, "X1", null, TestDatabase.PieceUnitId, null, true));

        var result = await Import.PreviewAsync(Workbook(Sheet("Bestand", StockHeaders,
            ["K-1", "Kunde 9", 1, null],
            ["K-1", null, 1, null],
            ["K-1", "Kunde 1", -1, null],
            ["K-1", "Kunde 1", "1,5", null],
            ["PC-1", "Kunde 1", 1, null],
            ["X-9", "Kunde 1", 1, null],
            ["K-1", "Kunde 2", null, null])), ImportMode.Set);

        Assert.All(result.Rows, r => Assert.Equal(ImportRowStatus.Error, r.Status));
        Assert.Contains("Unbekannter Kunde", result.Rows[0].Message);
        Assert.Equal(Messages.ImportCustomerMissing, result.Rows[1].Message);
        Assert.Equal(Messages.ImportNegativeQuantity, result.Rows[2].Message);
        Assert.Contains("ganze Zahl", result.Rows[3].Message);
        Assert.Contains("Seriennummern", result.Rows[4].Message);
        Assert.Contains("Kein passender Artikel", result.Rows[5].Message);
        Assert.Equal(Messages.ImportNothingToImport, result.Rows[6].Message);
    }

    [Fact]
    public async Task Devices_NewDevicesFollowTheMode()
    {
        await Articles.SaveAsync(null, new ArticleInput("PC-1", "PC", TestDatabase.SerialCategoryId, "DN", "X1", null, TestDatabase.PieceUnitId, null, true));

        await Import.ImportAsync(Workbook(Sheet("Geräte", DeviceHeaders, ["PC-1", null, null, "S1", "Kunde 1", "Defekt", "Kratzer"])), ImportMode.Set, "x.xlsx");
        await Import.ImportAsync(Workbook(Sheet("Geräte", DeviceHeaders, [null, "DN", "X1", "S2", "Kunde 1", null, null])), ImportMode.Add, "x.xlsx");

        var devices = await Devices.GetListAsync();
        Assert.Equal(DeviceState.Defective, devices.Single(d => d.SerialNumber == "S1").State);
        Assert.Equal(DeviceState.New, devices.Single(d => d.SerialNumber == "S2").State);
        Assert.Equal("Kratzer", (await Devices.GetDetailAsync(devices.Single(d => d.SerialNumber == "S1").Id)).Note);
        Assert.Single(await Journal.GetPageAsync(new MovementFilter(Type: MovementType.ImportCorrection), 0, 10));
        Assert.Single(await Journal.GetPageAsync(new MovementFilter(Type: MovementType.GoodsReceipt), 0, 10));
    }

    [Fact]
    public async Task Devices_ExistingDevice_StateChangeIsAMovement_OtherCustomerIsAnError()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "S1");
        await ReceiveDeviceAsync(Customer1, pc, "S2");

        var result = await Import.ImportAsync(Workbook(Sheet("Geräte", DeviceHeaders,
            [null, null, "X1", "s1", "Kunde 1", "Gebraucht", null],
            [null, null, "X1", "S2", "Kunde 2", null, null],
            [null, null, "X1", "S2", "Kunde 1", "Neu", null])), ImportMode.Set, "x.xlsx");

        Assert.Equal([ImportRowStatus.Updated, ImportRowStatus.Error, ImportRowStatus.Unchanged], result.Rows.Select(r => r.Status));
        Assert.Contains("zwischen Kunden", result.Rows[1].Message);
        Assert.Equal(DeviceState.UsedWorking, (await LoadDeviceAsync(deviceId)).State);
    }

    [Fact]
    public async Task Devices_IssuedStateOrIssuedDevice_AreErrors()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "S1"));

        var result = await Import.PreviewAsync(Workbook(Sheet("Geräte", DeviceHeaders,
            [null, null, "X1", "S1", "Kunde 1", null, null],
            [null, null, "X1", "S9", "Kunde 1", "Ausgegeben", null],
            [null, null, "X1", "S8", "Kunde 1", "Ausgemustert", null],
            [null, null, "X1", "S7", "Kunde 1", "kaputt", null])), ImportMode.Set);

        Assert.Equal(Messages.ImportDeviceIsIssued, result.Rows[0].Message);
        Assert.Equal(Messages.ImportIssuedNotAllowed, result.Rows[1].Message);
        Assert.Contains("nur Neu", result.Rows[2].Message);
        Assert.Contains("Unbekannter Zustand", result.Rows[3].Message);
    }

    [Fact]
    public async Task Devices_DeviceBackFromHeadquarters_ComesBackIntoStock()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "S1");
        await Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, deviceId, null, null, null));

        var result = await Import.ImportAsync(Workbook(Sheet("Geräte", DeviceHeaders, [null, null, "X1", "S1", "Kunde 1", null, null])), ImportMode.Add, "x.xlsx");

        Assert.Equal(ImportRowStatus.Updated, Assert.Single(result.Rows).Status);
        Assert.Equal(DeviceState.New, (await LoadDeviceAsync(deviceId)).State);
    }

    [Fact]
    public async Task ArticlesCreatedInTheSameFile_CanReceiveStockAndDevices()
    {
        var workbook = Workbook(
            Sheet("Artikel", ArticleHeaders,
                ["K-1", "USB-Kabel", "Kabel", null, null, null, null, null],
                ["PC-1", "Kassenrechner", "Kassenrechner", "DN", "X1", null, null, null]),
            Sheet("Bestand", StockHeaders, ["K-1", "Kunde 1", 7, 2]),
            Sheet("Geräte", DeviceHeaders, ["PC-1", null, null, "S1", "Kunde 1", null, null]));

        var preview = await Import.PreviewAsync(workbook, ImportMode.Set);
        var result = await Import.ImportAsync(workbook, ImportMode.Set, "x.xlsx");

        Assert.Equal(preview.Rows.Select(r => r.Status), result.Rows.Select(r => r.Status));
        Assert.All(result.Rows, r => Assert.Equal(ImportRowStatus.New, r.Status));
        Assert.Equal(2, (await Stock.GetLinesAsync(Customer1)).Count);
    }

    [Fact]
    public void Read_IgnoresHeaderCaseAndSpacesAndEmptyRows()
    {
        var workbook = Workbook(Sheet(" bestand ", ["  artikelNUMMER", "KUNDE ", "menge"],
            ["K-1", "Kunde 1", 3],
            [null, null, null],
            ["K-2", "Kunde 1", 4]));

        Assert.Equal([2, 4], workbook.Stock.Rows.Select(r => r.RowNumber));
        Assert.Equal("K-2", workbook.Stock.Rows[1].Text("Artikelnummer"));
        Assert.Empty(workbook.Articles.Rows);
    }

    [Fact]
    public void Read_MissingRequiredColumn_Throws()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Workbook(Sheet("Geräte", ["Artikelnummer", "Seriennummer"], ["A", "S"])));

        Assert.Contains("Kunde", ex.Message);
    }

    [Fact]
    public void Read_NotAnExcelFile_Throws()
    {
        using var stream = new MemoryStream("kein excel"u8.ToArray());

        var ex = Assert.Throws<BusinessRuleException>(() => ExcelWorkbook.Read(stream));
        Assert.Equal(Messages.ImportFileUnreadable, ex.Message);
    }
}
