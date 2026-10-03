using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Articles;

public enum ArticleDetailTab
{
    Stock,
    Devices,
    Movements,
}

/// <summary>Article with its stock per customer (and minimums), its devices and its movement history.</summary>
public sealed partial class ArticleDetailViewModel(
    ArticleService articles,
    StockService stock,
    DeviceService devices,
    JournalService journal,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<ArticleDetailViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private const int MovementCount = 100;

    private int? _id;
    private int? _presetCustomerId;
    private bool _isLoaded;
    private IReadOnlyList<DeviceListItem> _allDevices = [];

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? ManufacturerAndModel { get; set; }

    [ObservableProperty]
    public partial string? Details { get; set; }

    [ObservableProperty]
    public partial bool IsSerial { get; set; }

    [ObservableProperty]
    public partial bool IsInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStockTab), nameof(IsDevicesTab), nameof(IsMovementsTab))]
    public partial ArticleDetailTab SelectedTab { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StockLine>? StockLines { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial bool InStoreOnly { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DeviceListItem>? Devices { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MovementListItem>? Movements { get; set; }

    public bool IsStockTab => SelectedTab == ArticleDetailTab.Stock;

    public bool IsDevicesTab => SelectedTab == ArticleDetailTab.Devices;

    public bool IsMovementsTab => SelectedTab == ArticleDetailTab.Movements;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _id = ReadId(query);
        _presetCustomerId = query.TryGetValue(Routes.CustomerIdParameter, out var value) && value is int customerId ? customerId : null;
    }

    // Reloads on every appearance (after editing or booking), keeping tab and filters.
    public Task LoadAsync() => _id is not { } id ? Task.CompletedTask : RunAsync(async () =>
    {
        var article = await articles.GetAsync(id);
        Title = article.Name;
        ManufacturerAndModel = Labels.ManufacturerAndModel(article.Manufacturer, article.Model);
        Details = string.Join(" · ", new[]
        {
            article.Category!.Name,
            article.ArticleNumber is null ? null : $"Nr. {article.ArticleNumber}",
            article.Ean is null ? null : $"EAN {article.Ean}",
            article.Unit!.Name,
        }.Where(s => s is not null));
        IsSerial = article.Category.TrackingType == TrackingType.Serial;
        IsInactive = !article.IsActive;

        if (!_isLoaded)
        {
            CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
            SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == _presetCustomerId) ?? CustomerFilter.All;
            InStoreOnly = true;
            SelectedTab = IsSerial && _presetCustomerId is not null ? ArticleDetailTab.Devices : ArticleDetailTab.Stock;
            _isLoaded = true;
        }

        StockLines = await stock.GetArticleLinesAsync(id);
        _allDevices = IsSerial ? await devices.GetListAsync(articleId: id) : [];
        ApplyDeviceFilter();
        await LoadMovementsAsync();
    });

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value)
    {
        if (_isLoaded)
        {
            ApplyDeviceFilter();
            _ = RunAsync(LoadMovementsAsync);
        }
    }

    partial void OnInStoreOnlyChanged(bool value) => ApplyDeviceFilter();

    [RelayCommand]
    private void ShowTab(ArticleDetailTab tab) => SelectedTab = tab;

    [RelayCommand]
    private Task EditAsync() => _id is { } id ? navigation.GoToAsync(Routes.ArticleEdit, id) : Task.CompletedTask;

    [RelayCommand]
    private async Task SetMinimumAsync(StockLine line)
    {
        var text = await Dialogs.PromptAsync(
            "Mindestbestand",
            $"Mindestbestand von „{line.ArticleName}“ für {line.CustomerName} (leer = keiner):",
            line.Minimum?.ToString(CultureInfo.CurrentCulture),
            maxLength: 5);
        if (text is null)
        {
            return;
        }

        var saved = await RunAsync(() =>
        {
            int? minimum = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                minimum = int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
                    ? value
                    : throw new BusinessRuleException(Messages.Format(Messages.MinimumOutOfRange, StockService.MaxMinimum));
            }

            return stock.SetMinimumAsync(line.ArticleId, line.CustomerId, minimum);
        });

        if (saved)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync(DeviceListItem device) => navigation.GoToAsync(Routes.DeviceDetail, device.Id);

    [RelayCommand]
    private Task OpenMovementAsync(MovementListItem movement) => navigation.GoToAsync(Routes.MovementDetail, movement.Id);

    private void ApplyDeviceFilter()
    {
        var customerId = SelectedCustomerFilter?.CustomerId;
        Devices = [.. _allDevices.Where(d => (customerId is null || d.CustomerId == customerId) && (!InStoreOnly || d.IsInStore))];
    }

    private async Task LoadMovementsAsync()
    {
        if (_id is { } id)
        {
            Movements = await journal.GetPageAsync(new MovementFilter(SelectedCustomerFilter?.CustomerId, ArticleId: id), 0, MovementCount);
        }
    }
}
