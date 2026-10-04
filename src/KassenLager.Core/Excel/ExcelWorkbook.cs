using System.Globalization;
using MiniExcelLibs;

namespace KassenLager.Core.Excel;

/// <summary>A data row of an import sheet; cells are addressed by normalized header name.</summary>
public sealed class ImportRow(int rowNumber, IReadOnlyDictionary<string, object?> cells)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Row number as shown in Excel.</summary>
    public int RowNumber { get; } = rowNumber;

    /// <summary>Trimmed cell text; <c>null</c> for empty cells or missing columns.</summary>
    public string? Text(string header)
    {
        if (!cells.TryGetValue(ExcelWorkbook.NormalizeHeader(header), out var value) || value is null)
        {
            return null;
        }

        var text = value switch
        {
            double d => d.ToString("0.###############", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            DateTime date => date.ToString("dd.MM.yyyy", German),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
        text = text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// Whole number from a numeric cell or from text in German ("1.000") or invariant format;
    /// <c>null</c> for an empty cell.
    /// </summary>
    public int? Integer(string header)
    {
        if (!cells.TryGetValue(ExcelWorkbook.NormalizeHeader(header), out var value) || value is null)
        {
            return null;
        }

        switch (value)
        {
            case double d when d == Math.Floor(d) && d is >= int.MinValue and <= int.MaxValue:
                return (int)d;
            case int i:
                return i;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                return (int)l;
        }

        var text = Text(header);
        if (text is null)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, German, out var german)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out german))
        {
            return german;
        }

        throw new BusinessRuleException(Messages.Format(Messages.ImportNotAWholeNumber, header, text));
    }
}

public sealed record ImportSheet(string Name, IReadOnlyList<ImportRow> Rows);

/// <summary>
/// The import sheets of a workbook (Artikel, Bestand, Geräte); missing sheets are empty.
/// Header matching ignores case and surrounding spaces, empty rows are skipped.
/// </summary>
public sealed class ExcelWorkbook
{
    public const string ArticleSheet = "Artikel";
    public const string StockSheet = "Bestand";
    public const string DeviceSheet = "Geräte";
    public const string ListSheet = "Listen";

    public static readonly string[] ArticleHeaders = ["Artikelnummer", "Bezeichnung", "Kategorie", "Hersteller", "Modell", "EAN", "Einheit", "Notiz"];
    public static readonly string[] StockHeaders = ["Artikelnummer", "Kunde", "Menge", "Mindestbestand"];
    public static readonly string[] DeviceHeaders = ["Artikelnummer", "Hersteller", "Modell", "Seriennummer", "Kunde", "Zustand", "Notiz"];

    private ExcelWorkbook(ImportSheet articles, ImportSheet stock, ImportSheet devices)
    {
        Articles = articles;
        Stock = stock;
        Devices = devices;
    }

    public ImportSheet Articles { get; }

    public ImportSheet Stock { get; }

    public ImportSheet Devices { get; }

    public int RowCount => Articles.Rows.Count + Stock.Rows.Count + Devices.Rows.Count;

    public static string NormalizeHeader(string header) =>
        string.Join(' ', header.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static ExcelWorkbook Read(Stream source)
    {
        using var stream = new MemoryStream();
        source.CopyTo(stream);

        List<string> sheetNames;
        try
        {
            stream.Position = 0;
            sheetNames = MiniExcel.GetSheetNames(stream);
        }
        catch (Exception ex) when (ex is not BusinessRuleException)
        {
            throw new BusinessRuleException(Messages.ImportFileUnreadable);
        }

        var articles = ReadSheet(stream, sheetNames, ArticleSheet, ["Bezeichnung"]);
        var stock = ReadSheet(stream, sheetNames, StockSheet, ["Kunde"]);
        var devices = ReadSheet(stream, sheetNames, DeviceSheet, ["Seriennummer", "Kunde"], "Geraete");

        if (articles is null && stock is null && devices is null)
        {
            throw new BusinessRuleException(Messages.ImportNoSheets);
        }

        return new ExcelWorkbook(
            articles ?? new ImportSheet(ArticleSheet, []),
            stock ?? new ImportSheet(StockSheet, []),
            devices ?? new ImportSheet(DeviceSheet, []));
    }

    private static ImportSheet? ReadSheet(
        MemoryStream stream, List<string> sheetNames, string name, string[] requiredHeaders, string? alias = null)
    {
        var sheetName = sheetNames.FirstOrDefault(s =>
            NormalizeHeader(s) == NormalizeHeader(name) || (alias is not null && NormalizeHeader(s) == NormalizeHeader(alias)));
        if (sheetName is null)
        {
            return null;
        }

        List<IDictionary<string, object?>> raw;
        try
        {
            stream.Position = 0;
            raw = [.. MiniExcel.Query(stream, useHeaderRow: false, sheetName: sheetName, excelType: ExcelType.XLSX)
                .Cast<IDictionary<string, object?>>()];
        }
        catch (Exception ex) when (ex is not BusinessRuleException)
        {
            throw new BusinessRuleException(Messages.ImportFileUnreadable);
        }

        var headerIndex = raw.FindIndex(r => !IsEmpty(r));
        if (headerIndex < 0)
        {
            return new ImportSheet(name, []);
        }

        // Column letter → normalized header name.
        var columns = raw[headerIndex]
            .Where(c => c.Value is not null && !string.IsNullOrWhiteSpace(c.Value.ToString()))
            .ToDictionary(c => c.Key, c => NormalizeHeader(c.Value!.ToString()!));

        var missing = requiredHeaders.Where(h => !columns.ContainsValue(NormalizeHeader(h))).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException(Messages.Format(Messages.ImportMissingColumns, name, string.Join(", ", missing)));
        }

        var rows = new List<ImportRow>();
        for (var i = headerIndex + 1; i < raw.Count; i++)
        {
            if (IsEmpty(raw[i]))
            {
                continue;
            }

            var cells = new Dictionary<string, object?>();
            foreach (var (letter, header) in columns)
            {
                cells.TryAdd(header, raw[i].TryGetValue(letter, out var value) ? value : null);
            }

            rows.Add(new ImportRow(i + 1, cells));
        }

        return new ImportSheet(name, rows);
    }

    private static bool IsEmpty(IDictionary<string, object?> row) =>
        row.Values.All(v => v is null || string.IsNullOrWhiteSpace(v.ToString()));
}
