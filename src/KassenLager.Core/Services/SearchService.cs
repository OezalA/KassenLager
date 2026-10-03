using KassenLager.Core.Abstractions;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>An article found by the search, with its stock per customer (only customers with stock).</summary>
public sealed record ArticleSearchHit(ArticleListItem Article, IReadOnlyList<StockLine> Stock)
{
    public bool HasStock => Stock.Count > 0;
}

/// <param name="ExactDevice">The single device whose serial number equals the query, if any.</param>
public sealed record SearchResult(
    IReadOnlyList<ArticleSearchHit> Articles,
    IReadOnlyList<DeviceListItem> Devices,
    DeviceListItem? ExactDevice)
{
    public static SearchResult Empty { get; } = new([], [], null);

    public bool IsEmpty => Articles.Count == 0 && Devices.Count == 0;
}

/// <summary>
/// The main search: model, manufacturer, name, article number and EAN find articles with their
/// stock per customer; serial numbers find devices.
/// </summary>
public sealed class SearchService(IAppDbContextFactory dbFactory, ArticleService articles, StockService stock)
{
    public const int MaxArticles = 50;
    public const int MaxDevices = 30;

    public async Task<SearchResult> SearchAsync(string? text, int? customerId = null, CancellationToken ct = default)
    {
        var tokens = TextSearch.Tokenize(text);
        var key = TextKey.From(text);
        if (tokens.Length == 0 || key is null)
        {
            return SearchResult.Empty;
        }

        var matchingArticles = (await articles.GetListAsync(ct: ct))
            .Where(a => TextSearch.MatchesAll(tokens, a.Name, a.Manufacturer, a.Model, a.ArticleNumber, a.Ean))
            .ToList();

        var stockLines = matchingArticles.Count == 0
            ? []
            : await stock.GetLinesAsync(customerId, [.. matchingArticles.Select(a => a.Id)], ct);

        var hits = matchingArticles
            .Select(a => new ArticleSearchHit(a, [.. stockLines.Where(l => l.ArticleId == a.Id && l.Quantity > 0)]))
            .Where(h => h.Article.IsActive || h.HasStock)
            .OrderByDescending(h => h.HasStock)
            .ThenBy(h => h.Article.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxArticles)
            .ToList();

        await using var db = dbFactory.CreateDbContext();
        var devices = db.Devices.AsNoTracking().Where(d => !d.IsVoided);
        if (customerId is not null)
        {
            devices = devices.Where(d => d.CustomerId == customerId);
        }

        var deviceHits = await DeviceService.Project(devices
                .Where(d => d.SerialNumberKey.Contains(key))
                .OrderBy(d => d.SerialNumberKey.Length)
                .ThenBy(d => d.SerialNumberKey)
                .Take(MaxDevices))
            .ToListAsync(ct);

        var exact = await DeviceService.Project(devices.Where(d => d.SerialNumberKey == key).Take(2)).ToListAsync(ct);

        return new SearchResult(hits, deviceHits, exact.Count == 1 ? exact[0] : null);
    }
}
