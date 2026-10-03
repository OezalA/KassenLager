using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.BranchIssues;

/// <summary>History of devices left at branches; searchable by branch and both serial numbers.</summary>
public sealed partial class BranchIssueListViewModel(
    BranchService branches,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<BranchIssueListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private IReadOnlyList<BranchIssueListItem> _all = [];

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BranchIssueListItem>? Items { get; set; }

    [ObservableProperty]
    public partial string? CountText { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var customerId = SelectedCustomerFilter?.CustomerId;
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
        SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == customerId) ?? CustomerFilter.All;

        _all = await branches.GetListAsync();
        ApplyFilter();
    });

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value) => ApplyFilter();

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private Task OpenAsync(BranchIssueListItem issue) => navigation.GoToAsync(Routes.BranchIssueDetail, issue.Id);

    private void ApplyFilter()
    {
        var customerId = SelectedCustomerFilter?.CustomerId;
        Items = [.. _all.Where(i => (customerId is null || i.CustomerId == customerId) && i.Matches(FilterText))];
        CountText = Items.Count == 1 ? "1 Ausgabe" : $"{Items.Count} Ausgaben";
    }
}
