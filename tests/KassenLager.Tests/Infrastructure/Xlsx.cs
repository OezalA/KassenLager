using KassenLager.Core.Excel;
using MiniExcelLibs;

namespace KassenLager.Tests.Infrastructure;

/// <summary>Builds and reads small workbooks in memory.</summary>
public static class Xlsx
{
    public static ExcelSheet Sheet(string name, string[] headers, params object?[][] rows) =>
        new(name, headers, rows.Select(r => (IReadOnlyList<object?>)r));

    public static MemoryStream Build(params ExcelSheet[] sheets)
    {
        var stream = new MemoryStream();
        ExcelWriter.Write(stream, sheets);
        stream.Position = 0;
        return stream;
    }

    public static ExcelWorkbook Workbook(params ExcelSheet[] sheets)
    {
        using var stream = Build(sheets);
        return ExcelWorkbook.Read(stream);
    }

    public static List<string> SheetNames(MemoryStream stream)
    {
        stream.Position = 0;
        return MiniExcel.GetSheetNames(stream);
    }

    /// <summary>All rows of a sheet as cell texts (first row = header).</summary>
    public static List<string?[]> Rows(MemoryStream stream, string sheet)
    {
        stream.Position = 0;
        return [.. MiniExcel.Query(stream, useHeaderRow: false, sheetName: sheet, excelType: ExcelType.XLSX)
            .Cast<IDictionary<string, object?>>()
            .Select(r => r.Values.Select(v => v?.ToString()).ToArray())];
    }
}
