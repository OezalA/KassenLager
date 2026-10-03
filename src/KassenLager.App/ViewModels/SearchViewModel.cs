using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels;

/// <summary>
/// The main search. Results update while typing; pressing search on an exact serial number
/// opens the device directly (also the path for scanned codes later).
/// </summary>
public sealed partial class SearchViewModel(
    SearchService search,
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<SearchViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _pending;
    private bool _isLoaded;

    [ObservableProperty]
    public partial string? Query { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ArticleSearchHit>? Articles { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DeviceListItem>? Devices { get; set; }

    [ObservableProperty]
    public partial bool HasDevices { get; set; }

    [ObservableProperty]
    public partial bool HasArticles { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var selectedId = _isLoaded ? SelectedCustomerFilter?.CustomerId : preferences.SearchCustomerId;
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync(includeInactive: false));
        SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == selectedId) ?? CustomerFilter.All;
        _isLoaded = true;

        // Stock may have changed in the meantime (bookings on other tabs).
        await SearchNowAsync();
    });

    partial void OnQueryChanged(string? value) => _ = SearchAfterTypingAsync();

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value)
    {
        if (_isLoaded)
        {
            preferences.SearchCustomerId = value?.CustomerId;
            _ = SearchAfterTypingAsync();
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var result = await SearchNowAsync();
        if (result?.ExactDevice is { } device)
        {
            await navigation.GoToAsync(Routes.DeviceDetail, device.Id);
        }
    }

    [RelayCommand]
    private Task OpenArticleAsync(ArticleSearchHit hit) => OpenArticleForCustomerAsync(hit.Article.Id, SelectedCustomerFilter?.CustomerId);

    [RelayCommand]
    private Task OpenStockAsync(StockLine line) => OpenArticleForCustomerAsync(line.ArticleId, line.CustomerId);

    [RelayCommand]
    private Task OpenDeviceAsync(DeviceListItem device) => navigation.GoToAsync(Routes.DeviceDetail, device.Id);

    private Task OpenArticleForCustomerAsync(int articleId, int? customerId)
    {
        var parameters = new Dictionary<string, object> { [Routes.IdParameter] = articleId };
        if (customerId is not null)
        {
            parameters[Routes.CustomerIdParameter] = customerId.Value;
        }

        return navigation.GoToAsync(Routes.ArticleDetail, parameters);
    }

    private async Task SearchAfterTypingAsync()
    {
        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        try
        {
            await Task.Delay(TypingDelay, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchNowAsync();
    }

    private async Task<SearchResult?> SearchNowAsync()
    {
        _pending?.Cancel();
        var query = Query;
        SearchResult? result = null;
        if (!await RunAsync(async () => result = await search.SearchAsync(query, SelectedCustomerFilter?.CustomerId)) || query != Query)
        {
            return null;
        }

        Articles = result!.Articles;
        Devices = result.Devices;
        HasArticles = result.Articles.Count > 0;
        HasDevices = result.Devices.Count > 0;
        StatusText = string.IsNullOrWhiteSpace(query)
            ? "Modell, Hersteller, Bezeichnung, Artikelnummer, EAN oder Seriennummer eingeben."
            : result.IsEmpty ? "Keine Treffer." : null;
        return result;
    }
}
