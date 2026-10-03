using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Stock;

/// <summary>Stock lines of one category (group of the customer stock list).</summary>
public sealed class StockGroup(string name, IEnumerable<StockLine> lines) : List<StockLine>(lines)
{
    public string Name { get; } = name;
}

/// <summary>Stock of one customer grouped by category, with "below minimum" and "defective" filters.</summary>
public sealed partial class CustomerStockViewModel(
    StockService stock,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<CustomerStockViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _customerId;
    private IReadOnlyList<StockLine> _all = [];

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial bool BelowMinimumOnly { get; set; }

    [ObservableProperty]
    public partial bool DefectiveOnly { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StockGroup>? Groups { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _customerId = ReadId(query);

    public Task LoadAsync() => _customerId is not { } customerId ? Task.CompletedTask : RunAsync(async () =>
    {
        Title = (await customers.GetAsync(customerId)).Name;
        _all = await stock.GetLinesAsync(customerId);
        ApplyFilter();
    });

    partial void OnBelowMinimumOnlyChanged(bool value) => ApplyFilter();

    partial void OnDefectiveOnlyChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private Task OpenAsync(StockLine line) =>
        navigation.GoToAsync(Routes.ArticleDetail, new Dictionary<string, object>
        {
            [Routes.IdParameter] = line.ArticleId,
            [Routes.CustomerIdParameter] = line.CustomerId,
        });

    private void ApplyFilter()
    {
        var lines = _all.Where(l => (!BelowMinimumOnly || l.IsBelowMinimum) && (!DefectiveOnly || l.Defective > 0)).ToList();
        Groups = [.. lines.GroupBy(l => l.CategoryName).Select(g => new StockGroup(g.Key, g))];
        SummaryText = lines.Count == 1 ? "1 Artikel" : $"{lines.Count} Artikel";
    }
}
