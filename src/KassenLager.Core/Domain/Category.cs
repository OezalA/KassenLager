namespace KassenLager.Core.Domain;

/// <summary>Article category (Kategorie); decides whether articles are tracked by serial number or by quantity.</summary>
public class Category
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public TrackingType TrackingType { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Article> Articles { get; set; } = [];
}
