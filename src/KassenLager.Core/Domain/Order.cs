namespace KassenLager.Core.Domain;

/// <summary>Status of an order to headquarters. Values are persisted; never renumber.</summary>
public enum OrderStatus
{
    /// <summary>Entwurf: lines can still be changed.</summary>
    Draft = 1,

    /// <summary>Bestellt: sent to headquarters, nothing received yet.</summary>
    Ordered = 2,

    /// <summary>Teilweise geliefert.</summary>
    PartiallyDelivered = 3,

    /// <summary>Geliefert: everything received, or closed with the rest no longer expected.</summary>
    Delivered = 4,

    /// <summary>Storniert.</summary>
    Cancelled = 5,
}

public static class OrderStatuses
{
    /// <summary>Orders whose missing quantities still count as pending for the order suggestion.</summary>
    public static readonly OrderStatus[] Open = [OrderStatus.Draft, OrderStatus.Ordered, OrderStatus.PartiallyDelivered];
}

/// <summary>
/// Order to headquarters (Bestellung) for exactly one customer — each customer is handled by a
/// different department, so every order becomes its own Excel file.
/// </summary>
public class Order
{
    public const int ReferenceMaxLength = 100;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public OrderStatus Status { get; set; }

    /// <summary>Order number of headquarters, if known.</summary>
    public string? Reference { get; set; }

    public string? Note { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the order was placed (UTC).</summary>
    public DateTime? OrderedAt { get; set; }

    /// <summary>Set when a partially delivered order was closed without the rest (UTC).</summary>
    public DateTime? ClosedAt { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = [];
}

/// <summary>
/// One article of an order. The received quantity is not stored: it is the sum of the goods
/// receipt movements (and their reversals) that refer to the line.
/// </summary>
public class OrderLine
{
    public const int NoteMaxLength = 500;

    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public int ArticleId { get; set; }

    public Article? Article { get; set; }

    public int Quantity { get; set; }

    public string? Note { get; set; }

    public ICollection<Movement> Movements { get; set; } = [];
}
