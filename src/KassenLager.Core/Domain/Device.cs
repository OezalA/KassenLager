using KassenLager.Core.Text;

namespace KassenLager.Core.Domain;

/// <summary>
/// A single serial-tracked device (Gerät). Its <see cref="State"/> is changed by movements only;
/// every movement of the device records the state before and after.
/// </summary>
public class Device : IHasNormalizedKeys
{
    public const int SerialNumberMaxLength = 100;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    public int ArticleId { get; set; }

    public Article? Article { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Normalized <see cref="SerialNumber"/> for lookups and the unique index.</summary>
    public string SerialNumberKey { get; private set; } = string.Empty;

    public DeviceState State { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Set when the movement that created the device was reversed. The device keeps its
    /// history but no longer counts, and its serial number can be recorded again.
    /// </summary>
    public bool IsVoided { get; set; }

    /// <summary>When the device was first recorded (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Time of the device's latest movement (UTC).</summary>
    public DateTime StateChangedAt { get; set; }

    public ICollection<Movement> Movements { get; set; } = [];

    public void RefreshNormalizedKeys() => SerialNumberKey = TextKey.From(SerialNumber) ?? string.Empty;
}
