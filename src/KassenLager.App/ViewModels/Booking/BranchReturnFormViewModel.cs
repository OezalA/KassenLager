using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;
using Device = KassenLager.Core.Domain.Device;

namespace KassenLager.App.ViewModels.Booking;

/// <summary>
/// Rücknahme aus Filiale: the serial number is looked up while typing; a known device is
/// shown, an unknown one needs the article (model) to be created.
/// </summary>
public sealed partial class BranchReturnFormViewModel(
    BranchService branches,
    DeviceService devices,
    IPickerService pickers,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<BranchReturnFormViewModel> logger)
    : BookingFormViewModel(customers, preferences, navigation, dialogs, logger)
{
    private static readonly TimeSpan LookupDelay = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _lookup;

    public BranchInput Branch { get; } = new();

    public int SerialNumberMaxLength => Device.SerialNumberMaxLength;

    [ObservableProperty]
    public partial string? SerialNumber { get; set; }

    [ObservableProperty]
    public partial string? LookupText { get; set; }

    [ObservableProperty]
    public partial bool IsLookupWarning { get; set; }

    [ObservableProperty]
    public partial bool NeedsArticle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticle))]
    public partial ArticleListItem? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial bool IsUsedWorking { get; set; }

    [ObservableProperty]
    public partial bool IsDefective { get; set; }

    public bool HasArticle => SelectedArticle is not null;

    protected override async Task OnFirstLoadAsync()
    {
        IsUsedWorking = true;
        if (PresetDeviceId is { } deviceId)
        {
            var device = await devices.GetAsync(deviceId);
            await SelectCustomerAsync(device.CustomerId);
            SerialNumber = device.SerialNumber;
        }
    }

    protected override void OnCustomerChanged(Customer? customer)
    {
        _ = RunAsync(async () => Branch.SetKnownBranches(customer is null ? [] : await branches.GetBranchSuggestionsAsync(customer.Id)));
        _ = LookupAsync(TimeSpan.Zero);
    }

    partial void OnSerialNumberChanged(string? value) => _ = LookupAsync(LookupDelay);

    [RelayCommand]
    private async Task PickArticleAsync()
    {
        var article = await pickers.PickArticleAsync(new ArticlePickerOptions("Artikel (Modell) des Geräts", TrackingType.Serial, null));
        if (article is not null)
        {
            SelectedArticle = article;
        }
    }

    [RelayCommand]
    private Task BookAsync()
    {
        var input = new BranchReturnInput(
            SelectedCustomer?.Id,
            SerialNumber,
            null,
            NeedsArticle ? SelectedArticle?.Id : null,
            Branch.Text,
            IsDefective ? DeviceState.Defective : DeviceState.UsedWorking,
            Reference,
            Note,
            BookingDate);
        return BookAsync(() => branches.ReturnAsync(input), "Rücknahme gebucht");
    }

    private async Task LookupAsync(TimeSpan delay)
    {
        _lookup?.Cancel();
        var cts = _lookup = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // A newer lookup replaced this one.
        }

        IReadOnlyList<DeviceListItem> matches = [];
        if (await RunAsync(async () => matches = await devices.FindBySerialNumberAsync(SerialNumber)) && !cts.IsCancellationRequested)
        {
            ShowLookup(matches);
        }
    }

    private void ShowLookup(IReadOnlyList<DeviceListItem> matches)
    {
        IsLookupWarning = false;
        if (TextKey.Clean(SerialNumber) is null)
        {
            LookupText = null;
            NeedsArticle = false;
            return;
        }

        switch (matches.Count)
        {
            case 0:
                LookupText = "Unbekannte Seriennummer – das Gerät wird neu angelegt.";
                NeedsArticle = true;
                break;
            case 1:
                var device = matches[0];
                NeedsArticle = false;
                LookupText = $"{device.ArticleText}\n{device.CustomerName} · {device.StateName}";
                if (device.IsInStore)
                {
                    LookupText += "\nDas Gerät ist bereits im Lager.";
                    IsLookupWarning = true;
                }
                else if (device.CustomerId != SelectedCustomer?.Id)
                {
                    LookupText += "\nDas Gerät gehört zu einem anderen Kunden.";
                    IsLookupWarning = true;
                }

                break;
            default:
                LookupText = "Die Seriennummer ist bei mehreren Artikeln erfasst – bitte den Artikel wählen.";
                NeedsArticle = true;
                break;
        }
    }
}
