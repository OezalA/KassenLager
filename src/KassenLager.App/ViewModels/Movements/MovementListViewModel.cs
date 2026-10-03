using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Movements;

/// <summary>Buchungsjournal: all movements, newest first, loaded page by page.</summary>
public sealed partial class MovementListViewModel(
    JournalService journal,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<MovementListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private const int PageSize = 100;

    private bool _isLoaded;

    public IReadOnlyList<TypeFilter> TypeFilters => TypeFilter.Choices;

    public ObservableCollection<MovementListItem> Items { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial TypeFilter? SelectedTypeFilter { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        // Filter changes while rebuilding the pickers must not trigger a second reload.
        _isLoaded = false;
        var customerId = SelectedCustomerFilter?.CustomerId;
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
        SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == customerId) ?? CustomerFilter.All;
        SelectedTypeFilter ??= TypeFilters[0];
        _isLoaded = true;
        await ReloadAsync();
    });

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value) => ReloadIfLoaded();

    partial void OnSelectedTypeFilterChanged(TypeFilter? value) => ReloadIfLoaded();

    [RelayCommand]
    private Task LoadMoreAsync() => RunAsync(LoadPageAsync);

    [RelayCommand]
    private Task OpenAsync(MovementListItem movement) => navigation.GoToAsync(Routes.MovementDetail, movement.Id);

    private void ReloadIfLoaded()
    {
        if (_isLoaded)
        {
            _ = RunAsync(ReloadAsync);
        }
    }

    private Task ReloadAsync()
    {
        Items.Clear();
        return LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        var filter = new MovementFilter(SelectedCustomerFilter?.CustomerId, SelectedTypeFilter?.Type);
        var page = await journal.GetPageAsync(filter, Items.Count, PageSize + 1);
        foreach (var movement in page.Take(PageSize))
        {
            Items.Add(movement);
        }

        HasMore = page.Count > PageSize;
    }
}
