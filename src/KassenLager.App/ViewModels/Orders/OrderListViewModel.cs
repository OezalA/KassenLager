using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Orders;

/// <summary>Orders to headquarters, open ones by default.</summary>
public sealed partial class OrderListViewModel(
    OrderService orders,
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OrderListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private bool _isLoaded;

    [ObservableProperty]
    public partial bool OpenOnly { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<OrderListItem>? Items { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var wasLoaded = _isLoaded;
        _isLoaded = false;
        var customerId = SelectedCustomerFilter?.CustomerId;
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
        SelectedCustomerFilter = CustomerFilters.FirstOrDefault(f => f.CustomerId == customerId) ?? CustomerFilter.All;
        if (!wasLoaded)
        {
            OpenOnly = true;
        }

        _isLoaded = true;
        await ReloadAsync();
    });

    partial void OnOpenOnlyChanged(bool value) => ReloadIfLoaded();

    partial void OnSelectedCustomerFilterChanged(CustomerFilter? value) => ReloadIfLoaded();

    [RelayCommand]
    private Task OpenAsync(OrderListItem order) => navigation.GoToAsync(Routes.OrderDetail, order.Id);

    [RelayCommand]
    private Task OpenSuggestionAsync() => navigation.GoToAsync(Routes.OrderSuggestion);

    /// <summary>Empty draft for one customer; lines are added on the detail page.</summary>
    [RelayCommand]
    private async Task NewOrderAsync()
    {
        IReadOnlyList<Customer> list = [];
        if (!await RunAsync(async () => list = await customers.GetAllAsync(includeInactive: false)))
        {
            return;
        }

        var name = await Dialogs.ChooseAsync("Bestellung für Kunde", null, [.. list.Select(c => c.Name)]);
        if (list.FirstOrDefault(c => c.Name == name) is not { } customer)
        {
            return;
        }

        var id = 0;
        if (await RunAsync(async () => id = await orders.CreateAsync(customer.Id, [])))
        {
            await navigation.GoToAsync(Routes.OrderDetail, id);
        }
    }

    private void ReloadIfLoaded()
    {
        if (_isLoaded)
        {
            _ = RunAsync(ReloadAsync);
        }
    }

    private async Task ReloadAsync() => Items = await orders.GetListAsync(OpenOnly, SelectedCustomerFilter?.CustomerId);
}
