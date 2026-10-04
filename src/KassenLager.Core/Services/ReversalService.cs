using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>
/// Storno: a movement is cancelled by a linked reverse movement, never edited or deleted.
/// Each movement can be reversed once; reversals themselves cannot be reversed; a device
/// movement only while it is the device's latest effective movement, so the device's
/// history can be unwound step by step.
/// </summary>
public sealed class ReversalService(IAppDbContextFactory dbFactory, TimeProvider clock)
{
    /// <returns>The id of the reversal movement.</returns>
    public async Task<int> ReverseAsync(int movementId, string? note, CancellationToken ct = default)
    {
        var cleanNote = InputGuard.Optional(note, "Grund", Movement.NoteMaxLength);
        var now = clock.GetUtcNow().UtcDateTime;
        var time = new BookingTime(now, now);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var movement = await db.Movements.Include(m => m.Device).FirstOrDefaultAsync(m => m.Id == movementId, ct)
            ?? throw new EntityNotFoundException();

        var blocker = await ReversalRules.GetBlockerAsync(db, movement, ct);
        if (blocker is not null)
        {
            throw new BusinessRuleException(blocker);
        }

        Movement reversal;
        if (movement.Device is { } device)
        {
            // Back to the state before the movement; a movement that created the device voids it.
            reversal = Ledger.AddDeviceMovement(
                db, device, MovementType.Reversal, movement.ToState, movement.FromState, time, movement.Branch, movement.Reference, cleanNote);
            if (movement.FromState is null)
            {
                device.IsVoided = true;
            }

            var returnedIssue = await db.BranchIssues.FirstOrDefaultAsync(b => b.ReturnMovementId == movement.Id, ct);
            returnedIssue?.ReturnMovementId = null;
        }
        else
        {
            reversal = db.Movements.Add(new Movement
            {
                Type = MovementType.Reversal,
                OccurredAt = now,
                RecordedAt = now,
                CustomerId = movement.CustomerId,
                ArticleId = movement.ArticleId,
                QuantityChange = -movement.QuantityChange,
                Branch = movement.Branch,
                Reference = movement.Reference,
                Note = cleanNote,
            }).Entity;
        }

        reversal.ReversalOfId = movement.Id;

        // A reversed goods receipt of an order counts against the same order line.
        reversal.OrderLineId = movement.OrderLineId;

        await db.SaveChangesAsync(ct);
        if (movement.OrderLineId is { } orderLineId)
        {
            var orderId = await db.OrderLines.Where(l => l.Id == orderLineId).Select(l => l.OrderId).FirstAsync(ct);
            await OrderRules.RefreshStatusAsync(db, orderId, ct);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return reversal.Id;
    }
}

internal static class ReversalRules
{
    /// <summary>Why the movement cannot be reversed, or <c>null</c> if it can.</summary>
    public static async Task<string?> GetBlockerAsync(IAppDbContext db, Movement movement, CancellationToken ct)
    {
        if (movement.Type == MovementType.Reversal)
        {
            return Messages.ReversalOfReversal;
        }

        if (await db.Movements.AnyAsync(m => m.ReversalOfId == movement.Id, ct))
        {
            return Messages.AlreadyReversed;
        }

        if (movement.DeviceId is { } deviceId)
        {
            var latestEffective = await db.Movements
                .Where(m => m.DeviceId == deviceId && m.Type != MovementType.Reversal && m.ReversedBy == null)
                .MaxAsync(m => (int?)m.Id, ct);
            if (latestEffective != movement.Id)
            {
                return Messages.ReversalNotLatest;
            }

            var state = await db.Devices.Where(d => d.Id == deviceId).Select(d => d.State).FirstAsync(ct);
            return state == movement.ToState ? null : Messages.ReversalStateMismatch;
        }

        if (movement.QuantityChange > 0)
        {
            var stock = await Ledger.GetQuantityAsync(db, movement.ArticleId, movement.CustomerId, ct);
            if (stock < movement.QuantityChange)
            {
                var names = await db.Movements
                    .Where(m => m.Id == movement.Id)
                    .Select(m => new { Article = m.Article!.Name, Customer = m.Customer!.Name, Unit = m.Article.Unit!.Name })
                    .FirstAsync(ct);
                return Messages.Format(Messages.ReversalStockNegative, names.Article, names.Customer, stock, names.Unit);
            }
        }

        return null;
    }
}
