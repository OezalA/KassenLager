namespace KassenLager.Core.Domain;

/// <summary>
/// Entities with derived lookup columns (case-insensitive keys) that must be
/// refreshed whenever the entity is saved.
/// </summary>
public interface IHasNormalizedKeys
{
    void RefreshNormalizedKeys();
}
