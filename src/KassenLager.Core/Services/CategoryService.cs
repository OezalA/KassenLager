using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record CategoryInput(string? Name, TrackingType TrackingType, int SortOrder, bool IsActive);

public sealed record CategorySummary(int Id, string Name, TrackingType TrackingType, int SortOrder, bool IsActive, int ArticleCount);

public sealed class CategoryService(IAppDbContextFactory dbFactory)
{
    public async Task<IReadOnlyList<CategorySummary>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .Select(c => new CategorySummary(c.Id, c.Name, c.TrackingType, c.SortOrder, c.IsActive, c.Articles.Count))
            .ToListAsync(ct);

        return [.. categories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<CategorySummary> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategorySummary(c.Id, c.Name, c.TrackingType, c.SortOrder, c.IsActive, c.Articles.Count))
            .FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();
    }

    /// <summary>
    /// Creates or updates a category. The tracking type is locked once articles exist,
    /// because their stock would no longer match the way it was recorded.
    /// </summary>
    public async Task<int> SaveAsync(int? id, CategoryInput input, CancellationToken ct = default)
    {
        var name = InputGuard.Required(input.Name, "Name", Category.NameMaxLength);

        await using var db = dbFactory.CreateDbContext();

        var names = await db.Categories.Where(c => c.Id != id).Select(c => c.Name).ToListAsync(ct);
        if (names.Any(n => TextKey.EqualsIgnoreCase(n, name)))
        {
            throw new BusinessRuleException(Messages.Format(Messages.CategoryNameExists, name));
        }

        Category category;
        if (id is null)
        {
            category = db.Categories.Add(new Category()).Entity;
        }
        else
        {
            category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new EntityNotFoundException();
            if (category.TrackingType != input.TrackingType)
            {
                var articleCount = await db.Articles.CountAsync(a => a.CategoryId == category.Id, ct);
                if (articleCount > 0)
                {
                    throw new BusinessRuleException(Messages.Format(Messages.CategoryTrackingTypeLocked, articleCount));
                }
            }
        }

        category.Name = name;
        category.TrackingType = input.TrackingType;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new EntityNotFoundException();

        var articleCount = await db.Articles.CountAsync(a => a.CategoryId == id, ct);
        if (articleCount > 0)
        {
            throw new BusinessRuleException(Messages.Format(Messages.CategoryInUse, articleCount));
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }
}
