namespace KassenLager.Core.Text;

/// <summary>Text normalization shared by validation, lookups and unique keys.</summary>
public static class TextKey
{
    /// <summary>Trims the value; returns <c>null</c> for null or whitespace-only input.</summary>
    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Case-insensitive lookup key (Unicode-aware, unlike SQLite's NOCASE):
    /// trimmed and upper-cased; <c>null</c> for empty input.
    /// </summary>
    public static string? From(string? value) => Clean(value)?.ToUpperInvariant();

    public static bool EqualsIgnoreCase(string? a, string? b) => From(a) == From(b);
}
