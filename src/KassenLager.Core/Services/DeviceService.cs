using System.Linq.Expressions;
using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record DeviceListItem(
    int Id,
    string SerialNumber,
    DeviceState State,
    int ArticleId,
    string ArticleName,
    string? Manufacturer,
    string? Model,
    int CustomerId,
    string CustomerName,
    bool IsVoided,
    DateTime StateChangedAt)
{
    public string StateName => IsVoided ? "Storniert" : Labels.Of(State);

    public bool IsInStore => !IsVoided && State.IsInStore();

    public bool IsAvailable => !IsVoided && State.IsAvailable();

    public bool IsDefective => !IsVoided && State == DeviceState.Defective;

    public string ManufacturerAndModel => Labels.ManufacturerAndModel(Manufacturer, Model);

    public string ArticleText => $"{ArticleName} · {ManufacturerAndModel}";

    public DateTime StateChangedLocalTime => AppTime.ToLocal(StateChangedAt);

    public bool Matches(string? text) => TextSearch.Matches(text, SerialNumber, ArticleName, Manufacturer, Model);
}

public sealed record DeviceDetail(
    DeviceListItem Device,
    string CategoryName,
    string? Note,
    DateTime CreatedAt,
    IReadOnlyList<MovementListItem> History)
{
    public DateTime CreatedLocalTime => AppTime.ToLocal(CreatedAt);
}

public sealed class DeviceService(IAppDbContextFactory dbFactory)
{
    private static readonly Expression<Func<Device, DeviceListItem>> ToListItem = d => new DeviceListItem(
        d.Id,
        d.SerialNumber,
        d.State,
        d.ArticleId,
        d.Article!.Name,
        d.Article.Manufacturer,
        d.Article.Model,
        d.CustomerId,
        d.Customer!.Name,
        d.IsVoided,
        d.StateChangedAt);

    /// <summary>Non-voided devices, optionally filtered; sorted by serial number.</summary>
    public async Task<IReadOnlyList<DeviceListItem>> GetListAsync(
        int? customerId = null,
        int? articleId = null,
        IReadOnlyCollection<DeviceState>? states = null,
        CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Devices.AsNoTracking().Where(d => !d.IsVoided);
        if (customerId is not null)
        {
            query = query.Where(d => d.CustomerId == customerId);
        }

        if (articleId is not null)
        {
            query = query.Where(d => d.ArticleId == articleId);
        }

        if (states is not null)
        {
            query = query.Where(d => states.Contains(d.State));
        }

        return await query.OrderBy(d => d.SerialNumberKey).Select(ToListItem).ToListAsync(ct);
    }

    public async Task<DeviceListItem> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.Devices.AsNoTracking().Where(d => d.Id == id).Select(ToListItem).FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();
    }

    /// <summary>The device with its complete history (newest first), including the branches it was left at.</summary>
    public async Task<DeviceDetail> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var device = await db.Devices.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { Item = new DeviceListItem(
                d.Id, d.SerialNumber, d.State, d.ArticleId, d.Article!.Name, d.Article.Manufacturer, d.Article.Model,
                d.CustomerId, d.Customer!.Name, d.IsVoided, d.StateChangedAt),
                Category = d.Article.Category!.Name,
                d.Note,
                d.CreatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();

        // Entry order is the device's real sequence (back-dated bookings keep it).
        var history = await db.Movements.AsNoTracking()
            .Where(m => m.DeviceId == id)
            .OrderByDescending(m => m.Id)
            .Select(JournalService.ToListItem)
            .ToListAsync(ct);

        return new DeviceDetail(device.Item, device.Category, device.Note, device.CreatedAt, history);
    }

    /// <summary>Non-voided devices with exactly this serial number (case-insensitive), optionally of one article.</summary>
    public async Task<IReadOnlyList<DeviceListItem>> FindBySerialNumberAsync(
        string? serialNumber, int? articleId = null, CancellationToken ct = default)
    {
        var key = TextKey.From(serialNumber);
        if (key is null)
        {
            return [];
        }

        await using var db = dbFactory.CreateDbContext();
        return await db.Devices.AsNoTracking()
            .Where(d => d.SerialNumberKey == key && !d.IsVoided && (articleId == null || d.ArticleId == articleId))
            .Select(ToListItem)
            .ToListAsync(ct);
    }

    /// <summary>The note is the only freely editable field of a device.</summary>
    public async Task SetNoteAsync(int id, string? note, CancellationToken ct = default)
    {
        var cleanNote = InputGuard.Optional(note, "Notiz", Device.NoteMaxLength);

        await using var db = dbFactory.CreateDbContext();
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new EntityNotFoundException();
        device.Note = cleanNote;
        await db.SaveChangesAsync(ct);
    }

    internal static IQueryable<DeviceListItem> Project(IQueryable<Device> query) => query.Select(ToListItem);
}
