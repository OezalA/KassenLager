namespace KassenLager.Core.Domain;

/// <summary>Unit of measure (Einheit), e.g. Stück, Rolle, Packung.</summary>
public class Unit
{
    public const int NameMaxLength = 30;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Article> Articles { get; set; } = [];
}
