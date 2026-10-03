using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Booking;

/// <summary>Ausgabe an Filiale: an available device is left at a branch, usually as a loan unit.</summary>
public sealed partial class BranchIssueFormViewModel(
    BranchService branches,
    DeviceService devices,
    IPickerService pickers,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<BranchIssueFormViewModel> logger)
    : BookingFormViewModel(customers, preferences, navigation, dialogs, logger)
{
    public BranchInput Branch { get; } = new();

    public int CustomerDeviceSerialMaxLength => BranchIssue.CustomerDeviceSerialNumberMaxLength;

    public int CustomerDeviceModelMaxLength => BranchIssue.CustomerDeviceModelMaxLength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevice))]
    public partial DeviceListItem? SelectedDevice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerDeviceSerialLabel))]
    public partial bool IsLoan { get; set; }

    [ObservableProperty]
    public partial bool IsPermanent { get; set; }

    [ObservableProperty]
    public partial string? CustomerDeviceSerial { get; set; }

    [ObservableProperty]
    public partial string? CustomerDeviceModel { get; set; }

    [ObservableProperty]
    public partial bool IsCustomerDeviceSent { get; set; }

    [ObservableProperty]
    public partial DateTime? CustomerDeviceSentDate { get; set; }

    public bool HasDevice => SelectedDevice is not null;

    public string CustomerDeviceSerialLabel => IsLoan ? "Seriennummer Kundengerät *" : "Seriennummer Kundengerät";

    protected override async Task OnFirstLoadAsync()
    {
        IsLoan = true;
        CustomerDeviceSentDate = DateTime.Today;
        if (PresetDeviceId is { } deviceId)
        {
            var device = await devices.GetAsync(deviceId);
            await SelectCustomerAsync(device.CustomerId);
            SelectedDevice = device;
        }
    }

    protected override void OnCustomerChanged(Customer? customer)
    {
        if (SelectedDevice?.CustomerId != customer?.Id)
        {
            SelectedDevice = null;
        }

        _ = RunAsync(async () => Branch.SetKnownBranches(customer is null ? [] : await branches.GetBranchSuggestionsAsync(customer.Id)));
    }

    [RelayCommand]
    private async Task PickDeviceAsync()
    {
        if (SelectedCustomer is not { } customer)
        {
            return;
        }

        var device = await pickers.PickDeviceAsync(new DevicePickerOptions("Gerät ausgeben", customer.Id, DeviceStates.Available));
        if (device is not null)
        {
            SelectedDevice = device;
        }
    }

    [RelayCommand]
    private Task BookAsync()
    {
        var input = new BranchIssueInput(
            SelectedCustomer?.Id,
            SelectedDevice?.Id,
            Branch.Text,
            IsPermanent ? BranchIssueKind.PermanentInstallation : BranchIssueKind.Loan,
            CustomerDeviceSerial,
            CustomerDeviceModel,
            IsCustomerDeviceSent && CustomerDeviceSentDate is { } sent ? DateOnly.FromDateTime(sent) : null,
            Reference,
            Note,
            BookingDate);
        return BookAsync(() => branches.IssueAsync(input), "Ausgabe gebucht");
    }
}
