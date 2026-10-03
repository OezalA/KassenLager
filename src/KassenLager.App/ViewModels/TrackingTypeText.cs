using KassenLager.Core.Domain;

namespace KassenLager.App.ViewModels;

public sealed record TrackingTypeChoice(TrackingType Value, string Name);

public static class TrackingTypeText
{
    public static IReadOnlyList<TrackingTypeChoice> Choices { get; } =
    [
        new(TrackingType.Serial, "Seriennummer"),
        new(TrackingType.Quantity, "Menge"),
    ];

    public static string Of(TrackingType trackingType) => Choices.First(c => c.Value == trackingType).Name;
}
