namespace KassenLager.Core.Domain;

/// <summary>Key/value setting that belongs to the data (and therefore to backups).</summary>
public class AppSetting
{
    public const int KeyMaxLength = 100;
    public const int ValueMaxLength = 2000;

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}
