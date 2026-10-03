namespace KassenLager.Core.Domain;

/// <summary>
/// A stock movement (Bewegung). Movements are the source of truth for stock and are
/// immutable: mistakes are corrected with a linked reverse movement (Storno).
/// </summary>
public class Movement
{
    public const int BranchMaxLength = 100;
    public const int ReferenceMaxLength = 100;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    public MovementType Type { get; set; }

    /// <summary>Business time of the movement (UTC); may be back-dated by the user.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>When the movement was entered (UTC).</summary>
    public DateTime RecordedAt { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int ArticleId { get; set; }

    public Article? Article { get; set; }

    /// <summary>
    /// Signed change of the physical stock of the article for the customer. For devices it is
    /// +1, -1 or 0 depending on whether the device enters or leaves the store.
    /// </summary>
    public int QuantityChange { get; set; }

    public int? DeviceId { get; set; }

    public Device? Device { get; set; }

    /// <summary>Device state before the movement; <c>null</c> when the movement created the device.</summary>
    public DeviceState? FromState { get; set; }

    /// <summary>Device state after the movement; <c>null</c> when a reversal voided the device.</summary>
    public DeviceState? ToState { get; set; }

    /// <summary>Branch (Filiale) as free text.</summary>
    public string? Branch { get; set; }

    /// <summary>Ticket, order or delivery note number.</summary>
    public string? Reference { get; set; }

    public string? Note { get; set; }

    /// <summary>Set on a reversal: the movement it cancels.</summary>
    public int? ReversalOfId { get; set; }

    public Movement? ReversalOf { get; set; }

    /// <summary>The reversal that cancelled this movement, if any.</summary>
    public Movement? ReversedBy { get; set; }

    /// <summary>Details of an issue to a branch (only for <see cref="MovementType.BranchIssue"/>).</summary>
    public BranchIssue? BranchIssue { get; set; }
}
