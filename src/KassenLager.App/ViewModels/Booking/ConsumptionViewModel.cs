using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Booking;

/// <summary>Entnahme / Verbrauch of a quantity-tracked part, optionally for a branch and ticket.</summary>
public sealed partial class ConsumptionViewModel(
    BookingService bookings,
    BranchService branches,
    StockService stock,
    IPickerService pickers,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<ConsumptionViewModel> logger)
    : BookingFormViewModel(customers, preferences, navigation, dialogs, logger)
{
    public BranchInput Branch { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticle))]
    public partial ArticleListItem? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial string? StockText { get; set; }

    [ObservableProperty]
    public partial string? QuantityText { get; set; }

    public bool HasArticle => SelectedArticle is not null;

    protected override Task OnFirstLoadAsync()
    {
        QuantityText = "1";
        return Task.CompletedTask;
    }

    protected override void OnCustomerChanged(Customer? customer) => _ = RunAsync(async () =>
    {
        Branch.SetKnownBranches(customer is null ? [] : await branches.GetBranchSuggestionsAsync(customer.Id));
        await RefreshStockAsync();
    });

    [RelayCommand]
    private async Task PickArticleAsync()
    {
        var article = await pickers.PickArticleAsync(new ArticlePickerOptions("Teil für Entnahme", TrackingType.Quantity, SelectedCustomer?.Id));
        if (article is not null)
        {
            SelectedArticle = article;
            await RunAsync(RefreshStockAsync);
        }
    }

    [RelayCommand]
    private Task BookAsync()
    {
        _ = int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity);
        var input = new ConsumptionInput(SelectedCustomer?.Id, SelectedArticle?.Id, quantity, Branch.Text, Reference, Note, BookingDate);
        return BookAsync(() => bookings.ConsumeAsync(input), "Entnahme gebucht");
    }

    private async Task RefreshStockAsync()
    {
        StockText = SelectedArticle is { } article && SelectedCustomer is { } customer
            ? $"Aktueller Bestand: {await stock.GetQuantityAsync(article.Id, customer.Id)} {article.UnitName}"
            : null;
    }
}
