using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <param name="Date">Booking date; <c>null</c> or today books "now".</param>
public sealed record QuantityReceiptInput(int? CustomerId, int? ArticleId, int Quantity, string? Reference, string? Note, DateOnly? Date);

public sealed record DeviceReceiptInput(
    int? CustomerId,
    int? ArticleId,
    IReadOnlyList<string> SerialNumbers,
    DeviceState State,
    string? Reference,
    string? Note,
    DateOnly? Date);

public sealed record ConsumptionInput(
    int? CustomerId,
    int? ArticleId,
    int Quantity,
    string? Branch,
    string? Reference,
    string? Note,
    DateOnly? Date);

public sealed record DeviceActionInput(int? CustomerId, int? DeviceId, string? Reference, string? Note, DateOnly? Date);

/// <summary>
/// Goods receipts, consumption and the device bookings that do not involve a branch.
/// Branch issues and returns are in <see cref="BranchService"/>, reversals in <see cref="ReversalService"/>.
/// </summary>
public sealed class BookingService(IAppDbContextFactory dbFactory, TimeProvider clock)
{
    /// <summary>Wareneingang of a quantity-tracked article.</summary>
    public async Task<int> ReceiveQuantityAsync(QuantityReceiptInput input, CancellationToken ct = default)
    {
        var quantity = Ledger.ValidQuantity(input.Quantity);
        var reference = InputGuard.Optional(input.Reference, "Referenz", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var article = await Ledger.LoadArticleAsync(db, input.ArticleId, TrackingType.Quantity, ct);

        var movement = db.Movements.Add(new Movement
        {
            Type = MovementType.GoodsReceipt,
            OccurredAt = time.OccurredAt,
            RecordedAt = time.RecordedAt,
            CustomerId = customer.Id,
            ArticleId = article.Id,
            QuantityChange = quantity,
            Reference = reference,
            Note = note,
        }).Entity;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return movement.Id;
    }

    /// <summary>
    /// Wareneingang of serial-tracked devices, one movement per device. A serial number that is
    /// already known for the article is booked in again if the device is currently out of the
    /// store (e.g. back from repair) and belongs to the same customer.
    /// </summary>
    /// <returns>The ids of the received devices.</returns>
    public async Task<IReadOnlyList<int>> ReceiveDevicesAsync(DeviceReceiptInput input, CancellationToken ct = default)
    {
        if (!input.State.IsInStore())
        {
            throw new BusinessRuleException(Messages.Format(Messages.StateNotAllowed, Labels.Of(input.State)));
        }

        var serialNumbers = Ledger.CleanSerialNumbers(input.SerialNumbers);
        var reference = InputGuard.Optional(input.Reference, "Referenz", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var article = await Ledger.LoadArticleAsync(db, input.ArticleId, TrackingType.Serial, ct);

        var movements = await Ledger.ReceiveSerialNumbersAsync(db, article, customer, serialNumbers, input.State, time, reference, note, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return [.. movements.Select(m => m.Device!.Id)];
    }

    /// <summary>Entnahme / Verbrauch of a quantity-tracked article; the stock must not become negative.</summary>
    public async Task<int> ConsumeAsync(ConsumptionInput input, CancellationToken ct = default)
    {
        var quantity = Ledger.ValidQuantity(input.Quantity);
        var branch = InputGuard.Optional(input.Branch, "Filiale", Movement.BranchMaxLength);
        var reference = InputGuard.Optional(input.Reference, "Ticketnummer", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var article = await Ledger.LoadArticleAsync(db, input.ArticleId, TrackingType.Quantity, ct);

        var stock = await Ledger.GetQuantityAsync(db, article.Id, customer.Id, ct);
        if (stock < quantity)
        {
            throw new BusinessRuleException(Messages.Format(
                Messages.InsufficientStock, article.Name, customer.Name, stock, article.Unit!.Name));
        }

        var movement = db.Movements.Add(new Movement
        {
            Type = MovementType.Consumption,
            OccurredAt = time.OccurredAt,
            RecordedAt = time.RecordedAt,
            CustomerId = customer.Id,
            ArticleId = article.Id,
            QuantityChange = -quantity,
            Branch = branch,
            Reference = reference,
            Note = note,
        }).Entity;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return movement.Id;
    }

    /// <summary>Rücksendung an Zentrale: any device in the store, usually a defective one.</summary>
    public Task<int> ReturnToHeadquartersAsync(DeviceActionInput input, CancellationToken ct = default) =>
        MoveDeviceInStoreAsync(input, MovementType.ReturnToHeadquarters, _ => DeviceState.ReturnedToHeadquarters, ct);

    /// <summary>Zustandsänderung between Neu, Gebraucht – funktionsfähig and Defekt; the device stays in the store.</summary>
    public Task<int> ChangeDeviceStateAsync(DeviceActionInput input, DeviceState newState, CancellationToken ct = default)
    {
        if (!newState.IsInStore())
        {
            throw new BusinessRuleException(Messages.Format(Messages.StateNotAllowed, Labels.Of(newState)));
        }

        return MoveDeviceInStoreAsync(
            input,
            MovementType.StateChange,
            device => device.State != newState
                ? newState
                : throw new BusinessRuleException(Messages.Format(Messages.DeviceStateUnchanged, Labels.Of(newState))),
            ct);
    }

    /// <summary>Ausmusterung: the device is scrapped and leaves the stock.</summary>
    public Task<int> DisposeDeviceAsync(DeviceActionInput input, CancellationToken ct = default) =>
        MoveDeviceInStoreAsync(input, MovementType.Disposal, _ => DeviceState.Disposed, ct);

    private async Task<int> MoveDeviceInStoreAsync(
        DeviceActionInput input, MovementType type, Func<Device, DeviceState> targetState, CancellationToken ct)
    {
        var reference = InputGuard.Optional(input.Reference, "Referenz", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var device = await Ledger.LoadDeviceAsync(db, input.DeviceId, customer, ct);
        if (!device.State.IsInStore())
        {
            throw new BusinessRuleException(Messages.Format(Messages.DeviceNotInStock, device.SerialNumber, Labels.Of(device.State)));
        }

        var movement = Ledger.AddDeviceMovement(db, device, type, device.State, targetState(device), time, null, reference, note);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return movement.Id;
    }
}
