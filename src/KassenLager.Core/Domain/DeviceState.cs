namespace KassenLager.Core.Domain;

/// <summary>Condition and whereabouts of a serial-tracked device (Zustand).</summary>
public enum DeviceState
{
    /// <summary>In the store, unused.</summary>
    New = 1,

    /// <summary>In the store, used but working.</summary>
    UsedWorking = 2,

    /// <summary>In the store, broken.</summary>
    Defective = 3,

    /// <summary>Left at a branch; no longer in the store.</summary>
    Issued = 4,

    /// <summary>Sent back to headquarters (Zentrale).</summary>
    ReturnedToHeadquarters = 5,

    /// <summary>Scrapped.</summary>
    Disposed = 6,

    /// <summary>Not found during a stock count.</summary>
    Missing = 7,
}

public static class DeviceStates
{
    /// <summary>States of devices physically in the store (counted as stock).</summary>
    public static readonly DeviceState[] InStore = [DeviceState.New, DeviceState.UsedWorking, DeviceState.Defective];

    /// <summary>States of devices that can be used (verfügbar).</summary>
    public static readonly DeviceState[] Available = [DeviceState.New, DeviceState.UsedWorking];

    public static bool IsInStore(this DeviceState state) => state is DeviceState.New or DeviceState.UsedWorking or DeviceState.Defective;

    public static bool IsAvailable(this DeviceState state) => state is DeviceState.New or DeviceState.UsedWorking;

    /// <summary>Change of the physical stock when a device moves from one state to another.</summary>
    internal static int StockChange(DeviceState? from, DeviceState? to) =>
        (to?.IsInStore() == true ? 1 : 0) - (from?.IsInStore() == true ? 1 : 0);
}
