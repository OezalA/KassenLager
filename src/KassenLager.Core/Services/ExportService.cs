using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>Excel files: import template (Vorlage), Gesamtbestand, Bewegungsjournal and Ausgaben-Liste.</summary>
public sealed class ExportService(IAppDbContextFactory dbFactory, StockService stock, TimeProvider clock)
{
    public const string DateFormat = "dd.MM.yyyy";
    public const string TimeFormat = "HH:mm";

    /// <summary>File name with today's date, e.g. "Gesamtbestand_2026-10-04.xlsx".</summary>
    public string FileName(string prefix) => $"{prefix}_{AppTime.Today(clock):yyyy-MM-dd}.xlsx";

    /// <summary>Empty import sheets plus a "Listen" sheet with the valid categories, customers, states and units.</summary>
    public async Task WriteTemplateAsync(Stream stream, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct);
        var customers = (await db.Customers.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var units = (await db.Units.AsNoTracking().ToListAsync(ct)).OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var states = DeviceStates.InStore.Select(Labels.Of).ToList();

        var length = new[] { categories.Count, customers.Count, units.Count, states.Count }.Max();
        var lists = Enumerable.Range(0, length).Select(i => (IReadOnlyList<object?>)
        [
            i < categories.Count ? categories[i].Name : null,
            i < categories.Count ? (categories[i].TrackingType == TrackingType.Serial ? "Seriennummer" : "Menge") : null,
            i < customers.Count ? customers[i].Name : null,
            i < states.Count ? states[i] : null,
            i < units.Count ? units[i].Name : null,
        ]);

        ExcelWriter.Write(stream,
        [
            new ExcelSheet(ExcelWorkbook.ArticleSheet, ExcelWorkbook.ArticleHeaders, []),
            new ExcelSheet(ExcelWorkbook.StockSheet, ExcelWorkbook.StockHeaders, []),
            new ExcelSheet(ExcelWorkbook.DeviceSheet, ExcelWorkbook.DeviceHeaders, []),
            new ExcelSheet(ExcelWorkbook.ListSheet, ["Kategorie", "Erfassungsart", "Kunde", "Zustand", "Einheit"], lists),
        ]);
    }

    /// <summary>One sheet per customer (sorted by category) and a sheet with all devices in the store.</summary>
    public async Task WriteStockAsync(Stream stream, CancellationToken ct = default)
    {
        var lines = await stock.GetLinesAsync(ct: ct);

        await using var db = dbFactory.CreateDbContext();
        var customers = (await db.Customers.AsNoTracking().ToListAsync(ct))
            .Where(c => c.IsActive || lines.Any(l => l.CustomerId == c.Id))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var devices = await db.Devices.AsNoTracking()
            .Where(d => !d.IsVoided && DeviceStates.InStore.Contains(d.State))
            .Select(d => new { Customer = d.Customer!.Name, d.Article!.Manufacturer, d.Article.Model, d.Article.Name, d.SerialNumber, d.State })
            .ToListAsync(ct);

        var sheets = customers.Select(c => new ExcelSheet(
            c.Name,
            ["Kategorie", "Artikelnummer", "Bezeichnung", "Hersteller", "Modell", "Einheit", "Bestand", "Verfügbar", "Defekt", "Mindestbestand", "Unter Mindestbestand"],
            lines.Where(l => l.CustomerId == c.Id).Select(l => (IReadOnlyList<object?>)
            [
                l.CategoryName,
                l.ArticleNumber,
                l.ArticleName,
                l.Manufacturer,
                l.Model,
                l.UnitName,
                l.Quantity,
                l.IsSerial ? l.Available : null,
                l.IsSerial ? l.Defective : null,
                l.Minimum,
                l.IsBelowMinimum ? "Ja" : null,
            ]))).ToList();

        sheets.Add(new ExcelSheet(
            ExcelWorkbook.DeviceSheet,
            ["Kunde", "Hersteller", "Modell", "Seriennummer", "Zustand", "Bezeichnung"],
            devices
                .OrderBy(d => d.Customer, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.Model, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.SerialNumber, StringComparer.CurrentCultureIgnoreCase)
                .Select(d => (IReadOnlyList<object?>)[d.Customer, d.Manufacturer, d.Model, d.SerialNumber, Labels.Of(d.State), d.Name])));

        ExcelWriter.Write(stream, sheets);
    }

    /// <summary>All movements in the date range (Berlin dates, inclusive), oldest first.</summary>
    public async Task WriteJournalAsync(Stream stream, DateOnly from, DateOnly to, int? customerId, CancellationToken ct = default)
    {
        var (start, end) = UtcRange(from, to);

        await using var db = dbFactory.CreateDbContext();
        var query = db.Movements.AsNoTracking().Where(m => m.OccurredAt >= start && m.OccurredAt < end);
        if (customerId is not null)
        {
            query = query.Where(m => m.CustomerId == customerId);
        }

        var movements = await query
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.OccurredAt,
                m.Type,
                Customer = m.Customer!.Name,
                m.Article!.ArticleNumber,
                Article = m.Article.Name,
                m.Article.Model,
                Serial = m.Device!.SerialNumber,
                m.QuantityChange,
                Unit = m.Article.Unit!.Name,
                m.FromState,
                m.ToState,
                m.Branch,
                m.Reference,
                m.Note,
                IsReversed = m.ReversedBy != null,
                m.ReversalOfId,
            })
            .ToListAsync(ct);

        ExcelWriter.Write(stream,
        [
            new ExcelSheet(
                "Bewegungsjournal",
                ["Nr.", "Datum", "Uhrzeit", "Buchungsart", "Kunde", "Artikelnummer", "Bezeichnung", "Modell", "Seriennummer", "Menge", "Einheit",
                 "Zustand vorher", "Zustand nachher", "Filiale", "Referenz", "Notiz", "Storniert", "Storno von Nr."],
                movements.Select(m =>
                {
                    var local = AppTime.ToLocal(m.OccurredAt);
                    return (IReadOnlyList<object?>)
                    [
                        m.Id, local.ToString(DateFormat), local.ToString(TimeFormat), Labels.Of(m.Type), m.Customer, m.ArticleNumber,
                        m.Article, m.Model, m.Serial, m.QuantityChange, m.Unit,
                        m.FromState is { } fromState ? Labels.Of(fromState) : null,
                        m.ToState is { } toState ? Labels.Of(toState) : null,
                        m.Branch, m.Reference, m.Note, m.IsReversed ? "Ja" : null, m.ReversalOfId,
                    ];
                })),
        ]);
    }

    /// <summary>Issues to branches in the date range (reversed ones left out), oldest first.</summary>
    public async Task WriteBranchIssuesAsync(Stream stream, DateOnly from, DateOnly to, int? customerId, CancellationToken ct = default)
    {
        var (start, end) = UtcRange(from, to);

        await using var db = dbFactory.CreateDbContext();
        var query = db.BranchIssues.AsNoTracking()
            .Where(b => b.Movement!.ReversedBy == null && b.Movement.OccurredAt >= start && b.Movement.OccurredAt < end);
        if (customerId is not null)
        {
            query = query.Where(b => b.Movement!.CustomerId == customerId);
        }

        var issues = await query.OrderBy(b => b.Movement!.OccurredAt).ThenBy(b => b.Id).Select(BranchService.ToListItem).ToListAsync(ct);

        ExcelWriter.Write(stream,
        [
            new ExcelSheet(
                "Ausgaben",
                ["Datum", "Kunde", "Filiale", "Art", "Bezeichnung", "Hersteller", "Modell", "Seriennummer", "Kundengerät Seriennummer",
                 "Kundengerät Modell", "Kundengerät an Zentrale", "Ticketnummer", "Notiz", "Zurückgenommen am", "Zurückgenommen aus"],
                issues.Select(i => (IReadOnlyList<object?>)
                [
                    i.LocalTime.ToString(DateFormat), i.CustomerName, i.Branch, i.KindName, i.ArticleName, i.Manufacturer, i.Model,
                    i.DeviceSerialNumber, i.CustomerDeviceSerialNumber, i.CustomerDeviceModel, i.CustomerDeviceSentOn?.ToString(DateFormat),
                    i.Reference, i.Note, i.ReturnedLocalTime?.ToString(DateFormat), i.ReturnBranch,
                ])),
        ]);
    }

    private static (DateTime Start, DateTime End) UtcRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new BusinessRuleException(Messages.DateRangeInvalid);
        }

        return (AppTime.LocalToUtc(from.ToDateTime(TimeOnly.MinValue)), AppTime.LocalToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)));
    }
}
