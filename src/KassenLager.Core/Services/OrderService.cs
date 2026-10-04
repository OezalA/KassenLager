using System.Linq.Expressions;
using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>An article below its minimum stock with the quantity to order.</summary>
public sealed record OrderSuggestion(
    int ArticleId,
    string ArticleName,
    string? ArticleNumber,
    string? Manufacturer,
    string? Model,
    string CategoryName,
    TrackingType TrackingType,
    string UnitName,
    int Minimum,
    int UsableStock,
    int Pending,
    int SuggestedQuantity)
{
    public string ManufacturerAndModel => Labels.ManufacturerAndModel(Manufacturer, Model);

    public string DetailText => Pending > 0
        ? $"Mindestbestand {Minimum} · verfügbar {UsableStock} · bereits bestellt {Pending}"
        : $"Mindestbestand {Minimum} · verfügbar {UsableStock}";
}

public sealed record OrderListItem(
    int Id,
    int CustomerId,
    string CustomerName,
    OrderStatus Status,
    string? Reference,
    DateTime CreatedAt,
    DateTime? OrderedAt,
    int LineCount,
    int OrderedQuantity,
    int ReceivedQuantity)
{
    public string Title => $"Bestellung {Id}";

    public string StatusName => Labels.Of(Status);

    public bool IsOpen => OrderStatuses.Open.Contains(Status);

    public DateTime CreatedLocalTime => AppTime.ToLocal(CreatedAt);

    public DateTime? OrderedLocalTime => OrderedAt is { } at ? AppTime.ToLocal(at) : null;

    public string ProgressText => Status is OrderStatus.Draft or OrderStatus.Cancelled
        ? LineCount == 1 ? "1 Position" : $"{LineCount} Positionen"
        : $"{LineCount} Positionen · {ReceivedQuantity} von {OrderedQuantity} geliefert";
}

public sealed record OrderLineItem(
    int Id,
    int ArticleId,
    string ArticleName,
    string? ArticleNumber,
    string? Manufacturer,
    string? Model,
    TrackingType TrackingType,
    string UnitName,
    int Quantity,
    int Received,
    string? Note)
{
    public int Open => Math.Max(0, Quantity - Received);

    public bool IsSerial => TrackingType == TrackingType.Serial;

    public string ManufacturerAndModel => Labels.ManufacturerAndModel(Manufacturer, Model);

    public string QuantityText => $"{Quantity} {UnitName}";

    public string ProgressText => Received == 0 ? "noch nichts geliefert" : $"{Received} von {Quantity} geliefert";
}

public sealed record OrderDetail(OrderListItem Order, string? Note, IReadOnlyList<OrderLineItem> Lines)
{
    public bool IsDraft => Order.Status == OrderStatus.Draft;

    public bool CanPlace => IsDraft && Lines.Count > 0;

    public bool CanDelete => IsDraft;

    public bool CanReceive => Order.Status is OrderStatus.Ordered or OrderStatus.PartiallyDelivered;

    /// <summary>Only without any goods receipt (the status is then "Bestellt").</summary>
    public bool CanCancel => Order.Status == OrderStatus.Ordered;

    public bool CanClose => Order.Status == OrderStatus.PartiallyDelivered;

    public bool CanEditHeader => Order.Status != OrderStatus.Cancelled;
}

public sealed record OrderLineInput(int ArticleId, int Quantity);

/// <param name="Quantity">Received quantity of a quantity-tracked line; ignored for serial lines.</param>
/// <param name="SerialNumbers">Received serial numbers of a serial-tracked line.</param>
public sealed record OrderReceiptLine(int LineId, int Quantity, IReadOnlyList<string> SerialNumbers);

public sealed record OrderReceiptInput(int OrderId, IReadOnlyList<OrderReceiptLine> Lines, DeviceState State, string? Note, DateOnly? Date);

/// <summary>Order suggestion, orders per customer and goods receipt from an order.</summary>
public sealed class OrderService(IAppDbContextFactory dbFactory, StockService stock, TimeProvider clock)
{
    private static readonly Expression<Func<Order, OrderListItem>> ToListItem = o => new OrderListItem(
        o.Id,
        o.CustomerId,
        o.Customer!.Name,
        o.Status,
        o.Reference,
        o.CreatedAt,
        o.OrderedAt,
        o.Lines.Count,
        o.Lines.Sum(l => l.Quantity),
        o.Lines.SelectMany(l => l.Movements).Sum(m => m.QuantityChange));

    /// <summary>
    /// Articles of the customer below their minimum: suggested quantity = minimum − (usable stock
    /// + quantity still expected from open orders, drafts included). Only positive suggestions.
    /// </summary>
    public async Task<IReadOnlyList<OrderSuggestion>> GetSuggestionsAsync(int customerId, CancellationToken ct = default)
    {
        var lines = await stock.GetLinesAsync(customerId, ct: ct);

        await using var db = dbFactory.CreateDbContext();
        var pending = await PendingByArticleAsync(db, customerId, ct);

        return [.. lines
            .Where(l => l.Minimum is not null && l.IsArticleActive)
            .Select(l =>
            {
                var expected = pending.GetValueOrDefault(l.ArticleId);
                return new OrderSuggestion(
                    l.ArticleId, l.ArticleName, l.ArticleNumber, l.Manufacturer, l.Model, l.CategoryName, l.TrackingType, l.UnitName,
                    l.Minimum!.Value, l.UsableStock, expected, l.Minimum.Value - (l.UsableStock + expected));
            })
            .Where(s => s.SuggestedQuantity > 0)];
    }

    /// <summary>Creates a draft order; <paramref name="lines"/> may be empty.</summary>
    public async Task<int> CreateAsync(int? customerId, IReadOnlyList<OrderLineInput> lines, CancellationToken ct = default)
    {
        // The same article twice becomes one line with the total quantity.
        var merged = lines
            .GroupBy(l => l.ArticleId)
            .Select(g => new OrderLineInput(g.Key, Ledger.ValidQuantity(g.Sum(l => Ledger.ValidQuantity(l.Quantity)))))
            .ToList();

        await using var db = dbFactory.CreateDbContext();
        var customer = await Ledger.LoadCustomerAsync(db, customerId, ct);
        var articleIds = merged.Select(l => l.ArticleId).ToList();
        if (await db.Articles.CountAsync(a => articleIds.Contains(a.Id), ct) != articleIds.Count)
        {
            throw new EntityNotFoundException();
        }

        var order = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Draft,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            Lines = [.. merged.Select(l => new OrderLine { ArticleId = l.ArticleId, Quantity = l.Quantity })],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    /// <param name="openOnly">Only drafts, ordered and partially delivered orders.</param>
    public async Task<IReadOnlyList<OrderListItem>> GetListAsync(bool openOnly, int? customerId = null, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Orders.AsNoTracking();
        if (openOnly)
        {
            query = query.Where(o => OrderStatuses.Open.Contains(o.Status));
        }

        if (customerId is not null)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        return await query.OrderByDescending(o => o.Id).Select(ToListItem).ToListAsync(ct);
    }

    public async Task<int> GetOpenCountAsync(CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.Orders.CountAsync(o => OrderStatuses.Open.Contains(o.Status), ct);
    }

    public async Task<OrderDetail> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == id).Select(ToListItem).FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();
        var note = await db.Orders.Where(o => o.Id == id).Select(o => o.Note).FirstAsync(ct);

        var lines = await db.OrderLines.AsNoTracking()
            .Where(l => l.OrderId == id)
            .Select(l => new OrderLineItem(
                l.Id,
                l.ArticleId,
                l.Article!.Name,
                l.Article.ArticleNumber,
                l.Article.Manufacturer,
                l.Article.Model,
                l.Article.Category!.TrackingType,
                l.Article.Unit!.Name,
                l.Quantity,
                l.Movements.Sum(m => m.QuantityChange),
                l.Note))
            .ToListAsync(ct);

        return new OrderDetail(order, note, [.. lines.OrderBy(l => l.ArticleName, StringComparer.CurrentCultureIgnoreCase)]);
    }

    /// <summary>Adds, changes or (quantity 0) removes the line of an article in a draft.</summary>
    public async Task SetLineAsync(int orderId, int articleId, int quantity, string? note, CancellationToken ct = default)
    {
        var cleanNote = InputGuard.Optional(note, "Notiz", OrderLine.NoteMaxLength);
        if (quantity != 0)
        {
            Ledger.ValidQuantity(quantity);
        }

        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new EntityNotFoundException();
        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException(Messages.OrderNotDraft);
        }

        var line = order.Lines.FirstOrDefault(l => l.ArticleId == articleId);
        if (quantity == 0)
        {
            if (line is not null)
            {
                order.Lines.Remove(line);
                db.OrderLines.Remove(line);
            }
        }
        else
        {
            if (line is null)
            {
                if (!await db.Articles.AnyAsync(a => a.Id == articleId, ct))
                {
                    throw new EntityNotFoundException();
                }

                line = new OrderLine { ArticleId = articleId };
                order.Lines.Add(line);
            }

            line.Quantity = quantity;
            line.Note = cleanNote;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Order number of headquarters and note; editable until the order is cancelled.</summary>
    public async Task SetHeaderAsync(int orderId, string? reference, string? note, CancellationToken ct = default)
    {
        var cleanReference = InputGuard.Optional(reference, "Bestellnummer", Order.ReferenceMaxLength);
        var cleanNote = InputGuard.Optional(note, "Notiz", Order.NoteMaxLength);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new EntityNotFoundException();
        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException(Messages.OrderClosedForChanges);
        }

        order.Reference = cleanReference;
        order.Note = cleanNote;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Marks a draft as ordered (Bestellt); its lines are fixed from then on.</summary>
    public async Task PlaceAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new EntityNotFoundException();
        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException(Messages.OrderCannotPlace);
        }

        if (order.Lines.Count == 0)
        {
            throw new BusinessRuleException(Messages.OrderHasNoLines);
        }

        order.Status = OrderStatus.Ordered;
        order.OrderedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDraftAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new EntityNotFoundException();
        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException(Messages.OrderCannotDelete);
        }

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cancels an order that has not received anything (Storniert).</summary>
    public async Task CancelAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new EntityNotFoundException();
        var received = await db.Movements.Where(m => m.OrderLine!.OrderId == orderId).SumAsync(m => m.QuantityChange, ct);
        if (order.Status != OrderStatus.Ordered || received != 0)
        {
            throw new BusinessRuleException(Messages.OrderCannotCancel);
        }

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Closes a partially delivered order; the rest is no longer expected.</summary>
    public async Task CloseAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new EntityNotFoundException();
        if (order.Status != OrderStatus.PartiallyDelivered)
        {
            throw new BusinessRuleException(Messages.OrderCannotClose);
        }

        order.ClosedAt = clock.GetUtcNow().UtcDateTime;
        order.Status = OrderStatus.Delivered;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Wareneingang from an order: per line the received quantity, or the serial numbers of the
    /// received devices. Partial deliveries are possible; more than the open quantity is refused.
    /// </summary>
    /// <returns>The number of movements booked.</returns>
    public async Task<int> ReceiveAsync(OrderReceiptInput input, CancellationToken ct = default)
    {
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Article!).ThenInclude(a => a.Category)
            .Include(o => o.Lines).ThenInclude(l => l.Article!).ThenInclude(a => a.Unit)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId, ct)
            ?? throw new EntityNotFoundException();
        if (order.Status is not (OrderStatus.Ordered or OrderStatus.PartiallyDelivered))
        {
            throw new BusinessRuleException(Messages.OrderNotReceivable);
        }

        var received = await ReceivedByLineAsync(db, order.Id, ct);
        var reference = order.Reference ?? $"Bestellung {order.Id}";
        var movements = new List<Movement>();

        foreach (var receiptLine in input.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == receiptLine.LineId)
                ?? throw new BusinessRuleException(Messages.OrderLineUnknown);
            var article = line.Article!;
            var isSerial = article.Category!.TrackingType == TrackingType.Serial;
            var serialNumbers = isSerial ? Ledger.CleanSerialNumbers(receiptLine.SerialNumbers, required: false) : [];
            var count = isSerial ? serialNumbers.Count : receiptLine.Quantity;
            if (count == 0)
            {
                continue;
            }

            var open = Math.Max(0, line.Quantity - received.GetValueOrDefault(line.Id));
            if (count < 0)
            {
                throw new BusinessRuleException(Messages.Format(Messages.QuantityOutOfRange, Ledger.MaxQuantity));
            }

            if (count > open)
            {
                throw new BusinessRuleException(Messages.Format(Messages.OrderReceiptTooMuch, article.Name, open, article.Unit!.Name));
            }

            if (isSerial)
            {
                var deviceMovements = await Ledger.ReceiveSerialNumbersAsync(
                    db, article, order.Customer!, serialNumbers, input.State, time, reference, note, ct);
                deviceMovements.ForEach(m => m.OrderLine = line);
                movements.AddRange(deviceMovements);
            }
            else
            {
                movements.Add(db.Movements.Add(new Movement
                {
                    Type = MovementType.GoodsReceipt,
                    OccurredAt = time.OccurredAt,
                    RecordedAt = time.RecordedAt,
                    CustomerId = order.CustomerId,
                    ArticleId = article.Id,
                    QuantityChange = count,
                    Reference = reference,
                    Note = note,
                    OrderLine = line,
                }).Entity);
            }
        }

        if (movements.Count == 0)
        {
            throw new BusinessRuleException(Messages.OrderReceiptEmpty);
        }

        await db.SaveChangesAsync(ct);
        await OrderRules.RefreshStatusAsync(db, order.Id, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return movements.Count;
    }

    /// <summary>"Bestellung_Kunde-1_2026-10-04.xlsx".</summary>
    public async Task<string> GetFileNameAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var customer = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Customer!.Name).FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();
        var safe = new string([.. customer.Select(c => char.IsLetterOrDigit(c) ? c : '-')]).Trim('-');
        return $"Bestellung_{orderId}_{safe}_{AppTime.Today(clock):yyyy-MM-dd}.xlsx";
    }

    /// <summary>Excel for headquarters: Kunde, Artikelnummer, Bezeichnung, Hersteller, Modell, Menge, Einheit, Notiz.</summary>
    public async Task WriteExcelAsync(int orderId, Stream stream, CancellationToken ct = default)
    {
        var detail = await GetDetailAsync(orderId, ct);
        ExcelWriter.Write(stream,
        [
            new ExcelSheet(
                "Bestellung",
                ["Kunde", "Artikelnummer", "Bezeichnung", "Hersteller", "Modell", "Menge", "Einheit", "Notiz"],
                detail.Lines.Select(l => (IReadOnlyList<object?>)
                [
                    detail.Order.CustomerName, l.ArticleNumber, l.ArticleName, l.Manufacturer, l.Model, l.Quantity, l.UnitName, l.Note,
                ])),
        ]);
    }

    private static async Task<Dictionary<int, int>> PendingByArticleAsync(IAppDbContext db, int customerId, CancellationToken ct)
    {
        var lines = await db.OrderLines.AsNoTracking()
            .Where(l => l.Order!.CustomerId == customerId && OrderStatuses.Open.Contains(l.Order.Status))
            .Select(l => new { l.ArticleId, l.Quantity, Received = l.Movements.Sum(m => m.QuantityChange) })
            .ToListAsync(ct);

        return lines
            .GroupBy(l => l.ArticleId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.Received)));
    }

    private static Task<Dictionary<int, int>> ReceivedByLineAsync(IAppDbContext db, int orderId, CancellationToken ct) =>
        db.OrderLines
            .Where(l => l.OrderId == orderId)
            .Select(l => new { l.Id, Received = l.Movements.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(l => l.Id, l => l.Received, ct);
}

internal static class OrderRules
{
    /// <summary>
    /// Derives the delivery status from the receipts booked so far (call after saving them):
    /// nothing → Bestellt, some → Teilweise geliefert, all (or closed) → Geliefert.
    /// </summary>
    public static async Task RefreshStatusAsync(IAppDbContext db, int orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);
        if (order.Status is OrderStatus.Draft or OrderStatus.Cancelled)
        {
            return;
        }

        var lines = await db.OrderLines
            .Where(l => l.OrderId == orderId)
            .Select(l => new { l.Quantity, Received = l.Movements.Sum(m => m.QuantityChange) })
            .ToListAsync(ct);
        var anyReceived = lines.Any(l => l.Received > 0);
        if (!anyReceived)
        {
            order.ClosedAt = null;
        }

        order.Status = lines.All(l => l.Received >= l.Quantity) || (anyReceived && order.ClosedAt is not null)
            ? OrderStatus.Delivered
            : anyReceived ? OrderStatus.PartiallyDelivered : OrderStatus.Ordered;
    }
}
