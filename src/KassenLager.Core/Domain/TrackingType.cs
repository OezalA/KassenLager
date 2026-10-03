namespace KassenLager.Core.Domain;

/// <summary>How the stock of a category's articles is tracked.</summary>
public enum TrackingType
{
    /// <summary>Every device is tracked individually by its serial number.</summary>
    Serial = 1,

    /// <summary>Only the quantity is tracked.</summary>
    Quantity = 2,
}
