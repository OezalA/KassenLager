using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;
using Device = KassenLager.Core.Domain.Device;

namespace KassenLager.App.ViewModels.Booking;

public sealed record SerialEntry(string SerialNumber, string? Hint);

/// <summary>Wareneingang: a quantity, or one serial number per device.</summary>
public sealed partial class GoodsReceiptViewModel(
    BookingService bookings,
    DeviceService devices,
    StockService stock,
    IPickerService pickers,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<GoodsReceiptViewModel> logger)
    : BookingFormViewModel(customers, preferences, navigation, dialogs, logger)
{
    public IReadOnlyList<StateChoice> StateChoices { get; } =
        [.. DeviceStates.InStore.Select(s => new StateChoice(s, Labels.Of(s)))];

    public int SerialNumberMaxLength => Device.SerialNumberMaxLength;

    public ObservableCollection<SerialEntry> SerialNumbers { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerial), nameof(IsQuantity), nameof(HasArticle))]
    public partial ArticleListItem? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial string? StockText { get; set; }

    [ObservableProperty]
    public partial string? QuantityText { get; set; }

    [ObservableProperty]
    public partial StateChoice? SelectedState { get; set; }

    [ObservableProperty]
    public partial string? SerialInput { get; set; }

    [ObservableProperty]
    public partial string? SerialCountText { get; set; }

    public bool HasArticle => SelectedArticle is not null;

    public bool IsSerial => SelectedArticle?.TrackingType == TrackingType.Serial;

    public bool IsQuantity => SelectedArticle?.TrackingType == TrackingType.Quantity;

    protected override Task OnFirstLoadAsync()
    {
        QuantityText = "1";
        SelectedState = StateChoices[0];
        return Task.CompletedTask;
    }

    protected override void OnCustomerChanged(Customer? customer) => _ = RefreshStockAsync();

    [RelayCommand]
    private async Task PickArticleAsync()
    {
        var article = await pickers.PickArticleAsync(new ArticlePickerOptions("Artikel für Wareneingang", null, SelectedCustomer?.Id));
        if (article is null || article.Id == SelectedArticle?.Id)
        {
            return;
        }

        SelectedArticle = article;
        SerialNumbers.Clear();
        UpdateSerialCount();
        await RefreshStockAsync();
    }

    [RelayCommand]
    private async Task AddSerialAsync()
    {
        var serialNumber = TextKey.Clean(SerialInput);
        if (serialNumber is null || SelectedArticle is not { } article)
        {
            return;
        }

        if (SerialNumbers.Any(s => TextKey.EqualsIgnoreCase(s.SerialNumber, serialNumber)))
        {
            await Dialogs.ToastAsync("Diese Seriennummer ist bereits in der Liste.");
            return;
        }

        IReadOnlyList<DeviceListItem> known = [];
        if (!await RunAsync(async () => known = await devices.FindBySerialNumberAsync(serialNumber, article.Id)))
        {
            return;
        }

        string? hint = null;
        if (known.FirstOrDefault() is { } existing)
        {
            // Warn and show the existing device; one that is out of the store can be received again.
            if (existing.IsInStore)
            {
                if (await Dialogs.ConfirmAsync(
                        "Bereits im Lager",
                        $"{existing.SerialNumber} ist bereits im Lager ({existing.CustomerName}, {existing.StateName}).",
                        "Gerät anzeigen",
                        "OK"))
                {
                    await Navigation.GoToAsync(Routes.DeviceDetail, existing.Id);
                }

                return;
            }

            if (!await Dialogs.ConfirmAsync(
                    "Seriennummer bekannt",
                    $"{existing.SerialNumber} ist bereits erfasst ({existing.CustomerName}, {existing.StateName}). Erneut einbuchen?",
                    "Einbuchen"))
            {
                return;
            }

            hint = $"bekannt, zuletzt „{existing.StateName}“ – wird erneut eingebucht";
        }

        SerialNumbers.Insert(0, new SerialEntry(serialNumber, hint));
        SerialInput = null;
        UpdateSerialCount();
    }

    [RelayCommand]
    private void RemoveSerial(SerialEntry entry)
    {
        SerialNumbers.Remove(entry);
        UpdateSerialCount();
    }

    [RelayCommand]
    private Task BookAsync()
    {
        if (IsSerial)
        {
            var input = new DeviceReceiptInput(
                SelectedCustomer?.Id,
                SelectedArticle?.Id,
                [.. SerialNumbers.Select(s => s.SerialNumber).Reverse()],
                SelectedState?.State ?? DeviceState.New,
                Reference,
                Note,
                BookingDate);
            var count = SerialNumbers.Count;
            return BookAsync(() => bookings.ReceiveDevicesAsync(input), count == 1 ? "1 Gerät eingebucht" : $"{count} Geräte eingebucht");
        }

        _ = int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity);
        return BookAsync(
            () => bookings.ReceiveQuantityAsync(new QuantityReceiptInput(SelectedCustomer?.Id, SelectedArticle?.Id, quantity, Reference, Note, BookingDate)),
            "Wareneingang gebucht");
    }

    private void UpdateSerialCount() =>
        SerialCountText = SerialNumbers.Count switch
        {
            0 => null,
            1 => "1 Seriennummer",
            var n => $"{n} Seriennummern",
        };

    private async Task RefreshStockAsync()
    {
        if (SelectedArticle is not { } article || SelectedCustomer is not { } customer)
        {
            StockText = null;
            return;
        }

        await RunAsync(async () =>
        {
            var line = (await stock.GetLinesAsync(customer.Id, [article.Id])).FirstOrDefault();
            StockText = $"Aktueller Bestand: {line?.StockText ?? "kein Bestand"}";
        });
    }
}
