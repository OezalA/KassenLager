using KassenLager.Core.Text;

namespace KassenLager.Core.Domain;

/// <summary>
/// Master data on model level (Artikel). An article is customer-independent;
/// stock is kept per article and customer.
/// </summary>
public class Article : IHasNormalizedKeys
{
    public const int ArticleNumberMaxLength = 50;
    public const int NameMaxLength = 200;
    public const int ManufacturerMaxLength = 100;
    public const int ModelMaxLength = 100;
    public const int EanMaxLength = 50;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    /// <summary>Order number used by the Zentrale. Optional, unique when set.</summary>
    public string? ArticleNumber { get; set; }

    /// <summary>Normalized <see cref="ArticleNumber"/> backing the unique index.</summary>
    public string? ArticleNumberKey { get; private set; }

    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? Ean { get; set; }

    public int UnitId { get; set; }

    public Unit? Unit { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public void RefreshNormalizedKeys() => ArticleNumberKey = TextKey.From(ArticleNumber);
}
