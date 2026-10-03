using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Devices;

/// <summary>All devices with serial number, state and customer filters (filtered in memory).</summary>
public sealed partial class DeviceListViewModel(
    DeviceService devices,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<DeviceListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private IReadOnlyList<DeviceListItem> _all = [];

    public IReadOnlyList<StateFilter> StateFilters => StateFilter.Choices;

    [ObservableProperty]
    public partial StateFilter? SelectedStateFilter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DeviceListItem>? Items { get; set; }

    [ObservableProperty]
    public partial string? CountText { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var customerId = SelectedCustomerFilter?.CustomerId;
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
        SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == customerId) ?? CustomerFilter.All;
        SelectedStateFilter ??= StateFilters[0];

        _all = await devices.GetListAsync();
        ApplyFilter();
    });

    partial void OnSelectedStateFilterChanged(StateFilter? value) => ApplyFilter();

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value) => ApplyFilter();

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private Task OpenAsync(DeviceListItem device) => navigation.GoToAsync(Routes.DeviceDetail, device.Id);

    private void ApplyFilter()
    {
        var states = SelectedStateFilter?.States;
        var customerId = SelectedCustomerFilter?.CustomerId;
        Items = [.. _all.Where(d =>
            (states is null || states.Contains(d.State))
            && (customerId is null || d.CustomerId == customerId)
            && d.Matches(FilterText))];
        CountText = Items.Count == 1 ? "1 Gerät" : $"{Items.Count} Geräte";
    }
}
