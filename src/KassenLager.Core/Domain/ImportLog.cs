namespace KassenLager.Core.Domain;

/// <summary>How the Bestand and Geräte sheets of an Excel import are applied.</summary>
public enum ImportMode
{
    /// <summary>"setzen": the sheet states the stock; differences are booked as Bestandskorrektur (Import).</summary>
    Set = 1,

    /// <summary>"addieren": the sheet lists received goods; quantities and devices are booked as Wareneingang.</summary>
    Add = 2,
}

/// <summary>Record of an executed Excel import.</summary>
public class ImportLog
{
    public const int FileNameMaxLength = 260;

    public int Id { get; set; }

    /// <summary>UTC.</summary>
    public DateTime ImportedAt { get; set; }

    public string FileName { get; set; } = string.Empty;

    public ImportMode Mode { get; set; }

    public int NewRows { get; set; }

    public int UpdatedRows { get; set; }

    public int UnchangedRows { get; set; }

    public int ErrorRows { get; set; }
}
