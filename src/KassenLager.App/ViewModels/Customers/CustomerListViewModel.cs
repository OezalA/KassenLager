using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Customers;

public sealed partial class CustomerListViewModel(
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<CustomerListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    [ObservableProperty]
    public partial IReadOnlyList<Customer>? Items { get; set; }

    public Task LoadAsync() => RunAsync(async () => Items = await customers.GetAllAsync());

    [RelayCommand]
    private Task AddAsync() => navigation.GoToAsync(Routes.CustomerEdit);

    [RelayCommand]
    private Task OpenAsync(Customer customer) => navigation.GoToAsync(Routes.CustomerEdit, customer.Id);
}
