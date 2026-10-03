using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Booking;

/// <summary>Device bookings without a branch that share one form.</summary>
public enum DeviceAction
{
    ReturnToHeadquarters = 1,
    StateChange = 2,
    Disposal = 3,
}

/// <summary>Rücksendung an Zentrale, Zustandsänderung or Ausmusterung of a device in the store.</summary>
public sealed partial class DeviceActionViewModel(
    BookingService bookings,
    DeviceService devices,
    IPickerService pickers,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<DeviceActionViewModel> logger)
    : BookingFormViewModel(customers, preferences, navigation, dialogs, logger)
{
    private DeviceAction _action = DeviceAction.ReturnToHeadquarters;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? ReferenceLabel { get; set; }

    [ObservableProperty]
    public partial string? Hint { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevice))]
    public partial DeviceListItem? SelectedDevice { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StateChoice>? StateChoices { get; set; }

    [ObservableProperty]
    public partial StateChoice? SelectedNewState { get; set; }

    public bool HasDevice => SelectedDevice is not null;

    public bool IsStateChange => _action == DeviceAction.StateChange;

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
        if (query.TryGetValue(Routes.ActionParameter, out var value) && value is DeviceAction action)
        {
            _action = action;
        }

        (Title, ReferenceLabel, Hint) = _action switch
        {
            DeviceAction.StateChange => ("Zustandsänderung", "Referenz / Ticketnummer", "Das Gerät bleibt im Lager."),
            DeviceAction.Disposal => ("Ausmusterung", "Referenz", "Das Gerät wird verschrottet und verlässt den Bestand."),
            _ => ("Rücksendung an Zentrale", "Referenz (z. B. RMA- oder Lieferscheinnummer)", "Das Gerät verlässt den Bestand."),
        };
        OnPropertyChanged(nameof(IsStateChange));
    }

    protected override async Task OnFirstLoadAsync()
    {
        if (PresetDeviceId is { } deviceId)
        {
            var device = await devices.GetAsync(deviceId);
            await SelectCustomerAsync(device.CustomerId);
            SetDevice(device);
        }
    }

    protected override void OnCustomerChanged(Customer? customer)
    {
        if (SelectedDevice?.CustomerId != customer?.Id)
        {
            SetDevice(null);
        }
    }

    [RelayCommand]
    private async Task PickDeviceAsync()
    {
        if (SelectedCustomer is not { } customer)
        {
            return;
        }

        var options = new DevicePickerOptions(
            "Gerät wählen", customer.Id, DeviceStates.InStore, DefectiveOnlyByDefault: _action == DeviceAction.ReturnToHeadquarters);
        if (await pickers.PickDeviceAsync(options) is { } device)
        {
            SetDevice(device);
        }
    }

    [RelayCommand]
    private Task BookAsync()
    {
        var input = new DeviceActionInput(SelectedCustomer?.Id, SelectedDevice?.Id, Reference, Note, BookingDate);
        return _action switch
        {
            DeviceAction.StateChange => BookAsync(
                () => bookings.ChangeDeviceStateAsync(input, SelectedNewState?.State ?? DeviceState.New),
                "Zustand geändert"),
            DeviceAction.Disposal => BookAsync(() => bookings.DisposeDeviceAsync(input), "Gerät ausgemustert"),
            _ => BookAsync(() => bookings.ReturnToHeadquartersAsync(input), "Rücksendung gebucht"),
        };
    }

    private void SetDevice(DeviceListItem? device)
    {
        SelectedDevice = device;
        StateChoices = [.. DeviceStates.InStore.Where(s => s != device?.State).Select(s => new StateChoice(s, Labels.Of(s)))];
        SelectedNewState = StateChoices.FirstOrDefault();
    }
}
