using System.Linq.Expressions;
using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record MovementListItem(
    int Id,
    MovementType Type,
    DateTime OccurredAt,
    int CustomerId,
    string CustomerName,
    int ArticleId,
    string ArticleName,
    string? Model,
    string UnitName,
    int QuantityChange,
    int? DeviceId,
    string? SerialNumber,
    DeviceState? FromState,
    DeviceState? ToState,
    string? Branch,
    string? Reference,
    string? Note,
    int? ReversalOfId,
    bool IsReversed)
{
    public DateTime LocalTime => AppTime.ToLocal(OccurredAt);

    public string TypeName => Labels.Of(Type);

    public string QuantityText => Labels.SignedQuantity(QuantityChange, UnitName);

    public string ArticleText => SerialNumber is null ? ArticleName : $"{ArticleName} · SN {SerialNumber}";

    /// <summary>State transition of a device movement, e.g. "Neu → Ausgegeben".</summary>
    public string? StateText => (FromState, ToState) switch
    {
        (null, null) => null,
        (null, { } to) => $"neu erfasst: {Labels.Of(to)}",
        ({ } from, null) => $"{Labels.Of(from)} → storniert",
        ({ } from, { } to) when from == to => Labels.Of(to),
        ({ } from, { } to) => $"{Labels.Of(from)} → {Labels.Of(to)}",
    };

    /// <summary>Customer, branch and reference in one line.</summary>
    public string DetailText => string.Join(" · ", new[] { CustomerName, Branch, Reference }.Where(s => !string.IsNullOrEmpty(s)));
}

public sealed record MovementDetail(
    MovementListItem Movement,
    DateTime RecordedAt,
    int? BranchIssueId,
    int? ReversedById,
    DateTime? ReversedAt,
    MovementType? ReversalOfType,
    DateTime? ReversalOfOccurredAt,
    string? ReversalBlocker)
{
    public bool CanReverse => ReversalBlocker is null;

    public DateTime RecordedLocalTime => AppTime.ToLocal(RecordedAt);
}

public sealed record MovementFilter(int? CustomerId = null, MovementType? Type = null, int? ArticleId = null, int? DeviceId = null);

/// <summary>Read access to the movement ledger (Buchungsjournal).</summary>
public sealed class JournalService(IAppDbContextFactory dbFactory)
{
    internal static readonly Expression<Func<Movement, MovementListItem>> ToListItem = m => new MovementListItem(
        m.Id,
        m.Type,
        m.OccurredAt,
        m.CustomerId,
        m.Customer!.Name,
        m.ArticleId,
        m.Article!.Name,
        m.Article.Model,
        m.Article.Unit!.Name,
        m.QuantityChange,
        m.DeviceId,
        m.Device!.SerialNumber,
        m.FromState,
        m.ToState,
        m.Branch,
        m.Reference,
        m.Note,
        m.ReversalOfId,
        m.ReversedBy != null);

    /// <summary>Movements matching the filter, newest first.</summary>
    public async Task<IReadOnlyList<MovementListItem>> GetPageAsync(
        MovementFilter filter, int skip, int take, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Movements.AsNoTracking();
        if (filter.CustomerId is not null)
        {
            query = query.Where(m => m.CustomerId == filter.CustomerId);
        }

        if (filter.Type is not null)
        {
            query = query.Where(m => m.Type == filter.Type);
        }

        if (filter.ArticleId is not null)
        {
            query = query.Where(m => m.ArticleId == filter.ArticleId);
        }

        if (filter.DeviceId is not null)
        {
            query = query.Where(m => m.DeviceId == filter.DeviceId);
        }

        return await query
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .Skip(skip)
            .Take(take)
            .Select(ToListItem)
            .ToListAsync(ct);
    }

    public async Task<MovementDetail> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var movement = await db.Movements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new EntityNotFoundException();

        var item = await db.Movements.AsNoTracking().Where(m => m.Id == id).Select(ToListItem).FirstAsync(ct);
        var branchIssueId = await db.BranchIssues.Where(b => b.MovementId == id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        var reversedBy = await db.Movements.AsNoTracking()
            .Where(m => m.ReversalOfId == id)
            .Select(m => new { m.Id, m.OccurredAt })
            .FirstOrDefaultAsync(ct);
        var reversalOf = movement.ReversalOfId is null
            ? null
            : await db.Movements.AsNoTracking()
                .Where(m => m.Id == movement.ReversalOfId)
                .Select(m => new { m.Type, m.OccurredAt })
                .FirstOrDefaultAsync(ct);

        return new MovementDetail(
            item,
            movement.RecordedAt,
            branchIssueId,
            reversedBy?.Id,
            reversedBy?.OccurredAt,
            reversalOf?.Type,
            reversalOf?.OccurredAt,
            await ReversalRules.GetBlockerAsync(db, movement, ct));
    }
}
