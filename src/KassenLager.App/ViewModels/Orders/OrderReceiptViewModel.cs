using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.App.ViewModels.Booking;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;
using Device = KassenLager.Core.Domain.Device;

namespace KassenLager.App.ViewModels.Orders;

/// <summary>One open order line on the receipt form: a quantity, or the serial numbers of the devices.</summary>
public sealed partial class ReceiptLineViewModel : ObservableObject
{
    public ReceiptLineViewModel(OrderLineItem line)
    {
        Line = line;
        // Full delivery is the common case; a smaller quantity is entered instead.
        QuantityText = line.IsSerial ? null : line.Open.ToString(CultureInfo.CurrentCulture);
        UpdateCount();
    }

    public OrderLineItem Line { get; }

    public int SerialNumberMaxLength => Device.SerialNumberMaxLength;

    public ObservableCollection<string> SerialNumbers { get; } = [];

    public string OpenText => $"offen: {Line.Open} {Line.UnitName}";

    [ObservableProperty]
    public partial string? QuantityText { get; set; }

    [ObservableProperty]
    public partial string? SerialInput { get; set; }

    [ObservableProperty]
    public partial string? CountText { get; set; }

    public int Quantity =>
        int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) ? quantity : 0;

    [RelayCommand]
    private void AddSerial()
    {
        var serialNumber = TextKey.Clean(SerialInput);
        if (serialNumber is not null && !SerialNumbers.Any(s => TextKey.EqualsIgnoreCase(s, serialNumber)))
        {
            SerialNumbers.Insert(0, serialNumber);
        }

        SerialInput = null;
        UpdateCount();
    }

    [RelayCommand]
    private void RemoveSerial(string serialNumber)
    {
        SerialNumbers.Remove(serialNumber);
        UpdateCount();
    }

    private void UpdateCount() =>
        CountText = Line.IsSerial ? $"{SerialNumbers.Count} von {Line.Open} erfasst" : null;
}

/// <summary>Wareneingang from an order: per open line the delivered quantity or serial numbers.</summary>
public sealed partial class OrderReceiptViewModel(
    OrderService orders,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OrderReceiptViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;
    private bool _isLoaded;

    public IReadOnlyList<StateChoice> StateChoices { get; } =
        [.. DeviceStates.InStore.Select(s => new StateChoice(s, Labels.Of(s)))];

    public ObservableCollection<ReceiptLineViewModel> Lines { get; } = [];

    public DateTime MaximumDate => DateTime.Today;

    public int NoteMaxLength => Movement.NoteMaxLength;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial bool HasSerialLines { get; set; }

    [ObservableProperty]
    public partial StateChoice? SelectedState { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _id = ReadId(query);

    public Task LoadAsync() => _isLoaded || _id is not { } id ? Task.CompletedTask : RunAsync(async () =>
    {
        var detail = await orders.GetDetailAsync(id);
        Title = $"Wareneingang · {detail.Order.Title}";
        foreach (var line in detail.Lines.Where(l => l.Open > 0))
        {
            Lines.Add(new ReceiptLineViewModel(line));
        }

        HasSerialLines = Lines.Any(l => l.Line.IsSerial);
        SelectedState = StateChoices[0];
        Date = DateTime.Today;
        _isLoaded = true;
    });

    [RelayCommand]
    private async Task BookAsync()
    {
        if (_id is not { } id)
        {
            return;
        }

        var input = new OrderReceiptInput(
            id,
            [.. Lines.Select(l => new OrderReceiptLine(l.Line.Id, l.Quantity, [.. l.SerialNumbers]))],
            SelectedState?.State ?? DeviceState.New,
            Note,
            Date is { } date ? DateOnly.FromDateTime(date) : null);

        var count = 0;
        if (await RunAsync(async () => count = await orders.ReceiveAsync(input)))
        {
            await Dialogs.ToastAsync(count == 1 ? "1 Buchung erstellt" : $"{count} Buchungen erstellt");
            await navigation.GoBackAsync();
        }
    }
}
