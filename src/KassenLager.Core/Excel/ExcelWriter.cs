using MiniExcelLibs;

namespace KassenLager.Core.Excel;

/// <summary>A sheet to write: header row plus data rows (cells: string, number or null).</summary>
public sealed record ExcelSheet(string Name, IReadOnlyList<string> Headers, IEnumerable<IReadOnlyList<object?>> Rows);

/// <summary>Writes plain workbooks. Rows are dictionaries, which keeps MiniExcel free of reflection over own types (trimming).</summary>
internal static class ExcelWriter
{
    private const int MaxSheetNameLength = 31;
    private static readonly char[] InvalidSheetNameChars = ['[', ']', ':', '*', '?', '/', '\\'];

    public static void Write(Stream stream, IEnumerable<ExcelSheet> sheets)
    {
        var workbook = new Dictionary<string, object>();
        foreach (var sheet in sheets)
        {
            var rows = new List<Dictionary<string, object?>> { ToRow(sheet.Headers.Cast<object?>().ToList(), sheet.Headers.Count) };
            rows.AddRange(sheet.Rows.Select(r => ToRow(r, sheet.Headers.Count)));
            workbook[UniqueName(sheet.Name, workbook.Keys)] = rows;
        }

        MiniExcel.SaveAs(stream, workbook, printHeader: false, excelType: ExcelType.XLSX);
    }

    /// <summary>Excel sheet names: at most 31 characters, no []:*?/\ and unique (case-insensitive).</summary>
    internal static string UniqueName(string name, IEnumerable<string> existing)
    {
        var clean = new string([.. name.Select(c => InvalidSheetNameChars.Contains(c) ? '-' : c)]).Trim();
        if (clean.Length == 0)
        {
            clean = "Blatt";
        }

        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = clean.Length > MaxSheetNameLength ? clean[..MaxSheetNameLength] : clean;
        for (var i = 2; taken.Contains(candidate); i++)
        {
            var suffix = $" ({i})";
            candidate = (clean.Length + suffix.Length > MaxSheetNameLength ? clean[..(MaxSheetNameLength - suffix.Length)] : clean) + suffix;
        }

        return candidate;
    }

    private static Dictionary<string, object?> ToRow(IReadOnlyList<object?> cells, int columnCount)
    {
        var row = new Dictionary<string, object?>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            row[$"C{i:D2}"] = i < cells.Count ? cells[i] : null;
        }

        return row;
    }
}
