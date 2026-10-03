using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>
/// Stock of one article for one customer. For serial-tracked articles <see cref="Quantity"/>
/// is the number of devices in the store (available + defective).
/// </summary>
public sealed record StockLine(
    int ArticleId,
    string ArticleName,
    string? Manufacturer,
    string? Model,
    string? ArticleNumber,
    int CategoryId,
    string CategoryName,
    int CategorySortOrder,
    TrackingType TrackingType,
    string UnitName,
    bool IsArticleActive,
    int CustomerId,
    string CustomerName,
    int Available,
    int Defective,
    int Quantity,
    int? Minimum)
{
    public bool IsSerial => TrackingType == TrackingType.Serial;

    /// <summary>Compared with the minimum: available devices, or the quantity.</summary>
    public int UsableStock => IsSerial ? Available : Quantity;

    public bool IsBelowMinimum => Minimum is { } minimum && UsableStock < minimum;

    public string ManufacturerAndModel => Labels.ManufacturerAndModel(Manufacturer, Model);

    public string StockText => IsSerial
        ? Defective > 0 ? $"{Available} verfügbar · {Defective} defekt" : $"{Available} verfügbar"
        : $"{Quantity} {UnitName}";

    public string? MinimumText => Minimum is { } minimum ? $"Mindestbestand {minimum}" : null;
}

public sealed record CustomerStockSummary(
    int CustomerId,
    string CustomerName,
    int AvailableDevices,
    int DefectiveDevices,
    int QuantityArticles,
    int BelowMinimum)
{
    public string DevicesText => DefectiveDevices > 0
        ? $"Geräte: {AvailableDevices} verfügbar · {DefectiveDevices} defekt"
        : $"Geräte: {AvailableDevices} verfügbar";

    public string MaterialText => QuantityArticles == 1 ? "Material: 1 Artikel" : $"Material: {QuantityArticles} Artikel";

    public bool IsBelowMinimum => BelowMinimum > 0;

    public string BelowMinimumText => BelowMinimum == 1 ? "1 unter Mindestbestand" : $"{BelowMinimum} unter Mindestbestand";
}

/// <summary>
/// Stock figures derived from the ledger: quantities are the sum of movement changes,
/// device counts come from the device states that only movements change.
/// </summary>
public sealed class StockService(IAppDbContextFactory dbFactory)
{
    public const int MaxMinimum = 10_000;

    /// <summary>
    /// Stock lines that have stock, defective devices or a minimum, optionally restricted to
    /// one customer and/or some articles. Sorted by category, article and customer.
    /// </summary>
    public async Task<IReadOnlyList<StockLine>> GetLinesAsync(
        int? customerId = null, IReadOnlyCollection<int>? articleIds = null, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await LoadLinesAsync(db, customerId, articleIds, ct);
    }

    /// <summary>One line per customer for an article: every active customer, plus inactive ones with stock.</summary>
    public async Task<IReadOnlyList<StockLine>> GetArticleLinesAsync(int articleId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var lines = await LoadLinesAsync(db, null, [articleId], ct);

        var article = await db.Articles.AsNoTracking()
            .Where(a => a.Id == articleId)
            .Select(a => new ArticleInfo(
                a.Id, a.Name, a.Manufacturer, a.Model, a.ArticleNumber, a.CategoryId, a.Category!.Name,
                a.Category.SortOrder, a.Category.TrackingType, a.Unit!.Name, a.IsActive))
            .FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();

        var customers = await db.Customers.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
        var empty = customers
            .Where(c => lines.All(l => l.CustomerId != c.Id))
            .Select(c => article.ToLine(c.Id, c.Name, 0, 0, 0, null));

        return [.. lines.Concat(empty).OrderBy(l => l.CustomerName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Dashboard figures per customer: active customers, plus inactive ones that still have stock.</summary>
    public async Task<IReadOnlyList<CustomerStockSummary>> GetCustomerSummariesAsync(CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var lines = await LoadLinesAsync(db, null, null, ct);
        var customers = await db.Customers.AsNoTracking().ToListAsync(ct);

        return [.. customers
            .Select(c =>
            {
                var own = lines.Where(l => l.CustomerId == c.Id).ToList();
                return (Customer: c, Summary: new CustomerStockSummary(
                    c.Id,
                    c.Name,
                    own.Sum(l => l.Available),
                    own.Sum(l => l.Defective),
                    own.Count(l => !l.IsSerial && l.Quantity > 0),
                    own.Count(l => l.IsBelowMinimum)));
            })
            .Where(x => x.Customer.IsActive || x.Summary.AvailableDevices + x.Summary.DefectiveDevices + x.Summary.QuantityArticles > 0)
            .Select(x => x.Summary)
            .OrderBy(s => s.CustomerName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<int> GetQuantityAsync(int articleId, int customerId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await Ledger.GetQuantityAsync(db, articleId, customerId, ct);
    }

    /// <summary>Sets the minimum stock; <c>null</c> or 0 removes it.</summary>
    public async Task SetMinimumAsync(int articleId, int customerId, int? minimum, CancellationToken ct = default)
    {
        if (minimum is < 0 or > MaxMinimum)
        {
            throw new BusinessRuleException(Messages.Format(Messages.MinimumOutOfRange, MaxMinimum));
        }

        await using var db = dbFactory.CreateDbContext();
        if (!await db.Articles.AnyAsync(a => a.Id == articleId, ct) || !await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new EntityNotFoundException();
        }

        var existing = await db.MinimumStocks.FirstOrDefaultAsync(m => m.ArticleId == articleId && m.CustomerId == customerId, ct);
        if (minimum is null or 0)
        {
            if (existing is not null)
            {
                db.MinimumStocks.Remove(existing);
            }
        }
        else if (existing is null)
        {
            db.MinimumStocks.Add(new MinimumStock { ArticleId = articleId, CustomerId = customerId, Quantity = minimum.Value });
        }
        else
        {
            existing.Quantity = minimum.Value;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<List<StockLine>> LoadLinesAsync(
        IAppDbContext db, int? customerId, IReadOnlyCollection<int>? articleIds, CancellationToken ct)
    {
        var movements = db.Movements.AsNoTracking()
            .Where(m => m.Article!.Category!.TrackingType == TrackingType.Quantity);
        var devices = db.Devices.AsNoTracking()
            .Where(d => !d.IsVoided && DeviceStates.InStore.Contains(d.State));
        var minimums = db.MinimumStocks.AsNoTracking();

        if (customerId is not null)
        {
            movements = movements.Where(m => m.CustomerId == customerId);
            devices = devices.Where(d => d.CustomerId == customerId);
            minimums = minimums.Where(m => m.CustomerId == customerId);
        }

        if (articleIds is not null)
        {
            movements = movements.Where(m => articleIds.Contains(m.ArticleId));
            devices = devices.Where(d => articleIds.Contains(d.ArticleId));
            minimums = minimums.Where(m => articleIds.Contains(m.ArticleId));
        }

        var quantities = await movements
            .GroupBy(m => new { m.ArticleId, m.CustomerId })
            .Select(g => new { g.Key.ArticleId, g.Key.CustomerId, Quantity = g.Sum(m => m.QuantityChange) })
            .ToListAsync(ct);

        var deviceCounts = await devices
            .GroupBy(d => new { d.ArticleId, d.CustomerId, d.State })
            .Select(g => new { g.Key.ArticleId, g.Key.CustomerId, g.Key.State, Count = g.Count() })
            .ToListAsync(ct);

        var minimumList = await minimums.ToListAsync(ct);

        var keys = quantities.Where(q => q.Quantity != 0).Select(q => (q.ArticleId, q.CustomerId))
            .Concat(deviceCounts.Select(d => (d.ArticleId, d.CustomerId)))
            .Concat(minimumList.Select(m => (m.ArticleId, m.CustomerId)))
            .Distinct()
            .ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var keyArticleIds = keys.Select(k => k.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking()
            .Where(a => keyArticleIds.Contains(a.Id))
            .Select(a => new ArticleInfo(
                a.Id, a.Name, a.Manufacturer, a.Model, a.ArticleNumber, a.CategoryId, a.Category!.Name,
                a.Category.SortOrder, a.Category.TrackingType, a.Unit!.Name, a.IsActive))
            .ToDictionaryAsync(a => a.Id, ct);

        var keyCustomerIds = keys.Select(k => k.CustomerId).Distinct().ToList();
        var customerNames = await db.Customers.AsNoTracking()
            .Where(c => keyCustomerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return [.. keys
            .Select(key =>
            {
                var article = articles[key.ArticleId];
                var counts = deviceCounts.Where(d => d.ArticleId == key.ArticleId && d.CustomerId == key.CustomerId).ToList();
                var available = counts.Where(d => d.State.IsAvailable()).Sum(d => d.Count);
                var defective = counts.Where(d => d.State == DeviceState.Defective).Sum(d => d.Count);
                var quantity = article.TrackingType == TrackingType.Serial
                    ? available + defective
                    : quantities.FirstOrDefault(q => q.ArticleId == key.ArticleId && q.CustomerId == key.CustomerId)?.Quantity ?? 0;
                var minimum = minimumList.FirstOrDefault(m => m.ArticleId == key.ArticleId && m.CustomerId == key.CustomerId)?.Quantity;
                return article.ToLine(key.CustomerId, customerNames[key.CustomerId], available, defective, quantity, minimum);
            })
            .OrderBy(l => l.CategorySortOrder)
            .ThenBy(l => l.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.ArticleName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.CustomerName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private sealed record ArticleInfo(
        int Id,
        string Name,
        string? Manufacturer,
        string? Model,
        string? ArticleNumber,
        int CategoryId,
        string CategoryName,
        int CategorySortOrder,
        TrackingType TrackingType,
        string UnitName,
        bool IsActive)
    {
        public StockLine ToLine(int customerId, string customerName, int available, int defective, int quantity, int? minimum) =>
            new(Id, Name, Manufacturer, Model, ArticleNumber, CategoryId, CategoryName, CategorySortOrder, TrackingType,
                UnitName, IsActive, customerId, customerName, available, defective, quantity, minimum);
    }
}
