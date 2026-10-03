using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>Business and entry time of a booking (both UTC).</summary>
internal readonly record struct BookingTime(DateTime OccurredAt, DateTime RecordedAt);

/// <summary>Building blocks shared by the booking services. Callers own the transaction.</summary>
internal static class Ledger
{
    public const int MaxQuantity = 100_000;

    /// <summary>
    /// Today means "now". A past date is booked at noon local time, which keeps the
    /// day stable in every time zone; future dates are rejected.
    /// </summary>
    public static BookingTime ResolveTime(TimeProvider clock, DateOnly? date)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = AppTime.Today(clock);
        if (date is null || date == today)
        {
            return new BookingTime(now, now);
        }

        if (date > today)
        {
            throw new BusinessRuleException(Messages.DateInFuture);
        }

        return new BookingTime(AppTime.LocalToUtc(date.Value.ToDateTime(new TimeOnly(12, 0))), now);
    }

    public static int ValidQuantity(int quantity) =>
        quantity is >= 1 and <= MaxQuantity
            ? quantity
            : throw new BusinessRuleException(Messages.Format(Messages.QuantityOutOfRange, MaxQuantity));

    public static async Task<Customer> LoadCustomerAsync(IAppDbContext db, int? customerId, CancellationToken ct) =>
        customerId is null
            ? throw new BusinessRuleException(Messages.CustomerRequired)
            : await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) ?? throw new EntityNotFoundException();

    public static async Task<Article> LoadArticleAsync(IAppDbContext db, int? articleId, TrackingType trackingType, CancellationToken ct)
    {
        if (articleId is null)
        {
            throw new BusinessRuleException(Messages.ArticleRequired);
        }

        var article = await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Unit)
            .FirstOrDefaultAsync(a => a.Id == articleId, ct)
            ?? throw new EntityNotFoundException();

        if (article.Category!.TrackingType != trackingType)
        {
            var template = trackingType == TrackingType.Quantity ? Messages.ArticleIsSerialTracked : Messages.ArticleIsQuantityTracked;
            throw new BusinessRuleException(Messages.Format(template, article.Name));
        }

        return article;
    }

    /// <summary>Loads a device for a booking of <paramref name="customer"/>; it must not be voided.</summary>
    public static async Task<Device> LoadDeviceAsync(IAppDbContext db, int? deviceId, Customer customer, CancellationToken ct)
    {
        if (deviceId is null)
        {
            throw new BusinessRuleException(Messages.DeviceRequired);
        }

        var device = await db.Devices.Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == deviceId, ct)
            ?? throw new EntityNotFoundException();
        EnsureBookable(device, customer);
        return device;
    }

    /// <summary>Devices are never moved between customers, and voided devices are history only.</summary>
    public static void EnsureBookable(Device device, Customer customer)
    {
        if (device.IsVoided)
        {
            throw new BusinessRuleException(Messages.Format(Messages.DeviceVoided, device.SerialNumber));
        }

        if (device.CustomerId != customer.Id)
        {
            throw new BusinessRuleException(Messages.Format(
                Messages.DeviceCustomerMismatch, device.SerialNumber, device.Customer?.Name, customer.Name));
        }
    }

    public static async Task<int> GetQuantityAsync(IAppDbContext db, int articleId, int customerId, CancellationToken ct) =>
        await db.Movements
            .Where(m => m.ArticleId == articleId && m.CustomerId == customerId)
            .SumAsync(m => m.QuantityChange, ct);

    /// <summary>
    /// Records a movement of a device and applies its state change. <paramref name="fromState"/> is
    /// <c>null</c> when the movement creates the device; <paramref name="toState"/> is <c>null</c>
    /// when a reversal voids it (the state is kept for the history).
    /// </summary>
    public static Movement AddDeviceMovement(
        IAppDbContext db,
        Device device,
        MovementType type,
        DeviceState? fromState,
        DeviceState? toState,
        BookingTime time,
        string? branch,
        string? reference,
        string? note)
    {
        var occurredAt = time.OccurredAt;
        if (fromState is not null)
        {
            // Keep the device history in order: a back-dated booking may not precede the latest one.
            var lastDate = AppTime.ToLocalDate(device.StateChangedAt);
            if (AppTime.ToLocalDate(occurredAt) < lastDate)
            {
                throw new BusinessRuleException(Messages.Format(Messages.DateBeforeLastDeviceMovement, lastDate));
            }

            if (occurredAt <= device.StateChangedAt)
            {
                occurredAt = device.StateChangedAt.AddSeconds(1);
            }
        }

        var movement = new Movement
        {
            Type = type,
            OccurredAt = occurredAt,
            RecordedAt = time.RecordedAt,
            CustomerId = device.CustomerId,
            ArticleId = device.ArticleId,
            Device = device,
            FromState = fromState,
            ToState = toState,
            QuantityChange = DeviceStates.StockChange(fromState, toState),
            Branch = branch,
            Reference = reference,
            Note = note,
        };
        db.Movements.Add(movement);

        if (toState is { } state)
        {
            device.State = state;
        }

        device.StateChangedAt = occurredAt;
        return movement;
    }

    /// <summary>Creates a device and the movement that brings it into the system.</summary>
    public static Movement AddNewDevice(
        IAppDbContext db,
        Article article,
        Customer customer,
        string serialNumber,
        DeviceState state,
        MovementType type,
        BookingTime time,
        string? branch,
        string? reference,
        string? note)
    {
        var device = new Device
        {
            ArticleId = article.Id,
            CustomerId = customer.Id,
            SerialNumber = serialNumber,
            State = state,
            CreatedAt = time.OccurredAt,
            StateChangedAt = time.OccurredAt,
        };
        db.Devices.Add(device);
        return AddDeviceMovement(db, device, type, null, state, time, branch, reference, note);
    }
}
