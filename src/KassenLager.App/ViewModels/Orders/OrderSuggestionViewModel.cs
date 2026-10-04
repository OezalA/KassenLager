using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Orders;

/// <summary>A suggested article; the quantity can be changed or the row left out.</summary>
public sealed partial class SuggestionRow : ObservableObject
{
    public SuggestionRow(OrderSuggestion suggestion)
    {
        Suggestion = suggestion;
        IsIncluded = true;
        QuantityText = suggestion.SuggestedQuantity.ToString(CultureInfo.CurrentCulture);
    }

    public OrderSuggestion Suggestion { get; }

    [ObservableProperty]
    public partial bool IsIncluded { get; set; }

    [ObservableProperty]
    public partial string? QuantityText { get; set; }
}

/// <summary>Bestellvorschlag of one customer → draft order.</summary>
public sealed partial class OrderSuggestionViewModel(
    OrderService orders,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OrderSuggestionViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private bool _isLoaded;

    public ObservableCollection<SuggestionRow> Rows { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Customer>? Customers { get; set; }

    [ObservableProperty]
    public partial Customer? SelectedCustomer { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial bool HasRows { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        if (!_isLoaded)
        {
            Customers = await customers.GetAllAsync(includeInactive: false);
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == preferences.BookingCustomerId) ?? Customers.FirstOrDefault();
            _isLoaded = true;
        }

        await ReloadAsync();
    });

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        if (_isLoaded)
        {
            _ = RunAsync(ReloadAsync);
        }
    }

    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        var lines = Rows
            .Where(r => r.IsIncluded)
            .Select(r => new OrderLineInput(
                r.Suggestion.ArticleId,
                int.TryParse(r.QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) ? quantity : 0))
            .ToList();
        if (lines.Count == 0)
        {
            await Dialogs.ToastAsync("Keine Position ausgewählt.");
            return;
        }

        var id = 0;
        if (await RunAsync(async () => id = await orders.CreateAsync(SelectedCustomer?.Id, lines)))
        {
            preferences.BookingCustomerId = SelectedCustomer?.Id;
            await Dialogs.ToastAsync("Bestellentwurf angelegt");
            await navigation.GoToAsync(Routes.OrderDetail, id);
        }
    }

    private async Task ReloadAsync()
    {
        Rows.Clear();
        if (SelectedCustomer is { } customer)
        {
            foreach (var suggestion in await orders.GetSuggestionsAsync(customer.Id))
            {
                Rows.Add(new SuggestionRow(suggestion));
            }
        }

        HasRows = Rows.Count > 0;
        SummaryText = HasRows
            ? $"{Rows.Count} Artikel unter Mindestbestand (offene Bestellungen sind schon abgezogen)."
            : "Kein Artikel unter Mindestbestand – oder bereits ausreichend bestellt.";
    }
}
