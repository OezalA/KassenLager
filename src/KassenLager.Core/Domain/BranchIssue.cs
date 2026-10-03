namespace KassenLager.Core.Domain;

public enum BranchIssueKind
{
    /// <summary>Leihgerät: loan unit while the customer's device is repaired.</summary>
    Loan = 1,

    /// <summary>Dauerhafter Einbau: permanent installation.</summary>
    PermanentInstallation = 2,
}

/// <summary>
/// Details of an issue to a branch (Ausgabe an Filiale), kept for the history only.
/// Customer, device, branch, ticket and date live on the issuing <see cref="Movement"/>.
/// The customer's own device is never a <see cref="Device"/> and never enters the stock.
/// </summary>
public class BranchIssue
{
    public const int CustomerDeviceSerialNumberMaxLength = Device.SerialNumberMaxLength;
    public const int CustomerDeviceModelMaxLength = Article.ModelMaxLength;

    public int Id { get; set; }

    public int MovementId { get; set; }

    public Movement? Movement { get; set; }

    public BranchIssueKind Kind { get; set; }

    public string? CustomerDeviceSerialNumber { get; set; }

    public string? CustomerDeviceModel { get; set; }

    /// <summary>When the customer's device was sent to headquarters; can be entered later.</summary>
    public DateOnly? CustomerDeviceSentOn { get; set; }

    /// <summary>The return from the branch (Rücknahme) that took the device back ("zurückgenommen").</summary>
    public int? ReturnMovementId { get; set; }

    public Movement? ReturnMovement { get; set; }
}
