using KassenLager.Core.Domain;

namespace KassenLager.Data;

/// <summary>
/// Initial master data, inserted once by the InitialCreate migration.
/// Never change existing entries here: users edit them in the app afterwards.
/// </summary>
public static class SeedData
{
    // Placeholder names; the real customer names are entered in the app only.
    public static Customer[] Customers { get; } =
    [
        new() { Id = 1, Name = "Kunde 1", IsActive = true },
        new() { Id = 2, Name = "Kunde 2", IsActive = true },
        new() { Id = 3, Name = "Kunde 3", IsActive = true },
        new() { Id = 4, Name = "Kunde 4", IsActive = true },
    ];

    public static Category[] Categories { get; } =
    [
        NewCategory(1, "Kassenrechner", TrackingType.Serial),
        NewCategory(2, "Kassenrechner All-in-One / Backshop", TrackingType.Serial),
        NewCategory(3, "Hub", TrackingType.Serial),
        NewCategory(4, "Bedienermonitor (Touchmonitor)", TrackingType.Serial),
        NewCategory(5, "Kundenmonitor (Kundenanzeige)", TrackingType.Serial),
        NewCategory(6, "Tischscanner mit Waage", TrackingType.Serial),
        NewCategory(7, "Tischscanner ohne Waage", TrackingType.Serial),
        NewCategory(8, "Waage", TrackingType.Serial),
        NewCategory(9, "Bondrucker", TrackingType.Serial),
        NewCategory(10, "Geldlade", TrackingType.Serial),
        NewCategory(11, "Handscanner", TrackingType.Serial),
        NewCategory(12, "EC-Terminal", TrackingType.Serial),
        NewCategory(13, "Kabel", TrackingType.Quantity),
        NewCategory(14, "Geldladen-Ersatzteile", TrackingType.Quantity),
        NewCategory(15, "Verbrauchs- und Verschleißmaterial", TrackingType.Quantity),
        NewCategory(16, "Sonstiges", TrackingType.Quantity),
    ];

    public static Unit[] Units { get; } =
    [
        new() { Id = 1, Name = "Stück" },
        new() { Id = 2, Name = "Rolle" },
        new() { Id = 3, Name = "Packung" },
    ];

    private static Category NewCategory(int id, string name, TrackingType trackingType) =>
        new() { Id = id, Name = name, TrackingType = trackingType, SortOrder = id * 10, IsActive = true };
}
