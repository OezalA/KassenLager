using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record UnitSummary(int Id, string Name, int ArticleCount);

public sealed class UnitService(IAppDbContextFactory dbFactory)
{
    public async Task<IReadOnlyList<UnitSummary>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var units = await db.Units.AsNoTracking()
            .Select(u => new UnitSummary(u.Id, u.Name, u.Articles.Count))
            .ToListAsync(ct);

        return [.. units.OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<int> SaveAsync(int? id, string? name, CancellationToken ct = default)
    {
        var cleanName = InputGuard.Required(name, "Einheit", Unit.NameMaxLength);

        await using var db = dbFactory.CreateDbContext();

        var names = await db.Units.Where(u => u.Id != id).Select(u => u.Name).ToListAsync(ct);
        if (names.Any(n => TextKey.EqualsIgnoreCase(n, cleanName)))
        {
            throw new BusinessRuleException(Messages.Format(Messages.UnitNameExists, cleanName));
        }

        var unit = id is null
            ? db.Units.Add(new Unit()).Entity
            : await db.Units.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new EntityNotFoundException();

        unit.Name = cleanName;
        await db.SaveChangesAsync(ct);
        return unit.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new EntityNotFoundException();

        var articleCount = await db.Articles.CountAsync(a => a.UnitId == id, ct);
        if (articleCount > 0)
        {
            throw new BusinessRuleException(Messages.Format(Messages.UnitInUse, articleCount));
        }

        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
    }
}
