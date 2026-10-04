using KassenLager.Core.Domain;

namespace KassenLager.Core.Text;

/// <summary>German display names of domain values (UI, reports and import).</summary>
public static class Labels
{
    public static string Of(DeviceState state) => state switch
    {
        DeviceState.New => "Neu",
        DeviceState.UsedWorking => "Gebraucht – funktionsfähig",
        DeviceState.Defective => "Defekt",
        DeviceState.Issued => "Ausgegeben",
        DeviceState.ReturnedToHeadquarters => "An Zentrale zurückgesendet",
        DeviceState.Disposed => "Ausgemustert",
        DeviceState.Missing => "Nicht auffindbar",
        _ => state.ToString(),
    };

    public static string Of(MovementType type) => type switch
    {
        MovementType.GoodsReceipt => "Wareneingang",
        MovementType.Consumption => "Entnahme / Verbrauch",
        MovementType.BranchIssue => "Ausgabe an Filiale",
        MovementType.BranchReturn => "Rücknahme aus Filiale",
        MovementType.ReturnToHeadquarters => "Rücksendung an Zentrale",
        MovementType.StateChange => "Zustandsänderung",
        MovementType.Disposal => "Ausmusterung",
        MovementType.InventoryCorrection => "Inventurkorrektur",
        MovementType.ImportCorrection => "Bestandskorrektur (Import)",
        MovementType.Reversal => "Storno",
        _ => type.ToString(),
    };

    public static string Of(BranchIssueKind kind) => kind switch
    {
        BranchIssueKind.Loan => "Leihgerät",
        BranchIssueKind.PermanentInstallation => "Dauerhafter Einbau",
        _ => kind.ToString(),
    };

    public static string Of(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Entwurf",
        OrderStatus.Ordered => "Bestellt",
        OrderStatus.PartiallyDelivered => "Teilweise geliefert",
        OrderStatus.Delivered => "Geliefert",
        OrderStatus.Cancelled => "Storniert",
        _ => status.ToString(),
    };

    /// <summary>"Hersteller Modell", skipping empty parts.</summary>
    public static string ManufacturerAndModel(string? manufacturer, string? model) =>
        string.Join(" ", new[] { manufacturer, model }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Signed quantity with unit, e.g. "+3 Stück" or "−1 Stück".</summary>
    public static string SignedQuantity(int quantity, string unit) =>
        quantity switch
        {
            > 0 => $"+{quantity} {unit}",
            < 0 => $"−{-quantity} {unit}",
            _ => $"±0 {unit}",
        };
}
