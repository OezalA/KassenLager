namespace KassenLager.Core.Domain;

/// <summary>Kind of a stock movement (Bewegungstyp). Values are persisted; never renumber.</summary>
public enum MovementType
{
    /// <summary>Wareneingang: goods received from headquarters.</summary>
    GoodsReceipt = 1,

    /// <summary>Entnahme / Verbrauch: quantity item used for a repair.</summary>
    Consumption = 2,

    /// <summary>Ausgabe an Filiale: device left at a branch.</summary>
    BranchIssue = 3,

    /// <summary>Rücknahme aus Filiale: device taken back from a branch.</summary>
    BranchReturn = 4,

    /// <summary>Rücksendung an Zentrale: device sent back to headquarters.</summary>
    ReturnToHeadquarters = 5,

    /// <summary>Zustandsänderung: condition change of a device that stays in the store.</summary>
    StateChange = 6,

    /// <summary>Ausmusterung: device scrapped.</summary>
    Disposal = 7,

    /// <summary>Inventurkorrektur: difference posted when a stock count is closed.</summary>
    InventoryCorrection = 8,

    /// <summary>Bestandskorrektur (Import): stock set by an Excel import.</summary>
    ImportCorrection = 9,

    /// <summary>Storno: linked reverse movement of an earlier movement.</summary>
    Reversal = 10,
}
