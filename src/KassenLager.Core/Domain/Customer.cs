namespace KassenLager.Core.Domain;

/// <summary>A retail chain (Kunde). Every stock record belongs to exactly one customer.</summary>
public class Customer
{
    public const int NameMaxLength = 100;
    public const int ShortNameMaxLength = 20;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName;
}
