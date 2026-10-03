using KassenLager.Core.Domain;
using KassenLager.Core.Text;

namespace KassenLager.App.ViewModels;

/// <summary>Entry of a customer filter picker; <see cref="CustomerId"/> <c>null</c> = all customers.</summary>
public sealed record CustomerFilter(int? CustomerId, string Name)
{
    public static CustomerFilter All { get; } = new(null, "Alle Kunden");

    public static IReadOnlyList<CustomerFilter> Build(IEnumerable<Customer> customers) =>
        [All, .. customers.Select(c => new CustomerFilter(c.Id, c.Name))];
}

/// <summary>Entry of a device state filter; <see cref="States"/> <c>null</c> = all states.</summary>
public sealed record StateFilter(string Name, IReadOnlyList<DeviceState>? States)
{
    public static IReadOnlyList<StateFilter> Choices { get; } =
    [
        new("Im Lager", DeviceStates.InStore),
        new("Alle Zustände", null),
        .. Enum.GetValues<DeviceState>().Select(s => new StateFilter(Labels.Of(s), [s])),
    ];
}

/// <summary>Entry of a movement type filter; <see cref="Type"/> <c>null</c> = all types.</summary>
public sealed record TypeFilter(MovementType? Type, string Name)
{
    public static IReadOnlyList<TypeFilter> Choices { get; } =
        [new(null, "Alle Buchungsarten"), .. Enum.GetValues<MovementType>().Select(t => new TypeFilter(t, Labels.Of(t)))];
}
