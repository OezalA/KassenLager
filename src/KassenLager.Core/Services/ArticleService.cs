using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record ArticleInput(
    string? ArticleNumber,
    string? Name,
    int? CategoryId,
    string? Manufacturer,
    string? Model,
    string? Ean,
    int? UnitId,
    string? Note,
    bool IsActive);

public sealed record ArticleListItem(
    int Id,
    string? ArticleNumber,
    string Name,
    string? Manufacturer,
    string? Model,
    string? Ean,
    int CategoryId,
    string CategoryName,
    TrackingType TrackingType,
    string UnitName,
    bool IsActive)
{
    public string ManufacturerAndModel => Labels.ManufacturerAndModel(Manufacturer, Model);

    /// <summary>Case-insensitive partial match of every word over the identifying text fields.</summary>
    public bool Matches(string? text) => TextSearch.Matches(text, Name, Manufacturer, Model, ArticleNumber, Ean);
}

public sealed class ArticleService(IAppDbContextFactory dbFactory)
{
    public async Task<IReadOnlyList<ArticleListItem>> GetListAsync(
        int? categoryId = null, bool includeInactive = true, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Articles.AsNoTracking();
        if (categoryId is not null)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        var items = await ProjectToListItem(query).ToListAsync(ct);
        return [.. items.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<Article> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.Articles.AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Unit)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new EntityNotFoundException();
    }

    /// <summary>
    /// Finds another article with the same manufacturer and model (case-insensitive).
    /// A duplicate is allowed but the user should be warned before saving.
    /// </summary>
    public async Task<ArticleListItem?> FindDuplicateModelAsync(
        string? manufacturer, string? model, int? excludeId, CancellationToken ct = default)
    {
        if (TextKey.Clean(model) is null)
        {
            return null;
        }

        await using var db = dbFactory.CreateDbContext();
        var candidates = await ProjectToListItem(
                db.Articles.AsNoTracking().Where(a => a.Model != null && a.Id != excludeId))
            .ToListAsync(ct);

        return candidates.FirstOrDefault(a =>
            TextKey.EqualsIgnoreCase(a.Model, model) && TextKey.EqualsIgnoreCase(a.Manufacturer, manufacturer));
    }

    /// <summary>Creates (<paramref name="id"/> = null) or updates an article and returns its id.</summary>
    public async Task<int> SaveAsync(int? id, ArticleInput input, CancellationToken ct = default)
    {
        var name = InputGuard.Required(input.Name, "Bezeichnung", Article.NameMaxLength);
        var articleNumber = InputGuard.Optional(input.ArticleNumber, "Artikelnummer", Article.ArticleNumberMaxLength);
        var manufacturer = InputGuard.Optional(input.Manufacturer, "Hersteller", Article.ManufacturerMaxLength);
        var model = InputGuard.Optional(input.Model, "Modell", Article.ModelMaxLength);
        var ean = InputGuard.Optional(input.Ean, "EAN", Article.EanMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Article.NoteMaxLength);

        if (input.CategoryId is null)
        {
            throw new BusinessRuleException(Messages.ArticleCategoryRequired);
        }

        if (input.UnitId is null)
        {
            throw new BusinessRuleException(Messages.ArticleUnitRequired);
        }

        await using var db = dbFactory.CreateDbContext();

        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.CategoryId, ct)
            ?? throw new EntityNotFoundException();

        if (!await db.Units.AnyAsync(u => u.Id == input.UnitId, ct))
        {
            throw new EntityNotFoundException();
        }

        // Serial-tracked devices are searched by model name in the field, so it is mandatory.
        if (category.TrackingType == TrackingType.Serial && model is null)
        {
            throw new BusinessRuleException(Messages.ArticleModelRequired);
        }

        var articleNumberKey = TextKey.From(articleNumber);
        if (articleNumberKey is not null)
        {
            var owner = await db.Articles
                .Where(a => a.Id != id && a.ArticleNumberKey == articleNumberKey)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(ct);
            if (owner is not null)
            {
                throw new BusinessRuleException(Messages.Format(Messages.ArticleNumberExists, articleNumber, owner));
            }
        }

        var article = id is null
            ? db.Articles.Add(new Article()).Entity
            : await db.Articles.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new EntityNotFoundException();

        article.ArticleNumber = articleNumber;
        article.Name = name;
        article.CategoryId = category.Id;
        article.Manufacturer = manufacturer;
        article.Model = model;
        article.Ean = ean;
        article.UnitId = input.UnitId.Value;
        article.Note = note;
        article.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);
        return article.Id;
    }

    /// <summary>Only articles without history can be deleted; others are deactivated instead.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new EntityNotFoundException();
        if (await db.Movements.AnyAsync(m => m.ArticleId == id, ct) || await db.Devices.AnyAsync(d => d.ArticleId == id, ct)
            || await db.OrderLines.AnyAsync(l => l.ArticleId == id, ct))
        {
            throw new BusinessRuleException(Messages.ArticleHasHistory);
        }

        db.Articles.Remove(article);
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<ArticleListItem> ProjectToListItem(IQueryable<Article> query) =>
        query.Select(a => new ArticleListItem(
            a.Id,
            a.ArticleNumber,
            a.Name,
            a.Manufacturer,
            a.Model,
            a.Ean,
            a.CategoryId,
            a.Category!.Name,
            a.Category.TrackingType,
            a.Unit!.Name,
            a.IsActive));
}
