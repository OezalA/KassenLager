using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels;

public sealed partial class MoreViewModel(
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<MoreViewModel> logger)
    : ViewModelBase(dialogs, logger)
{
    [RelayCommand]
    private Task OpenRouteAsync(string route) => navigation.GoToAsync(route);

    [RelayCommand]
    private async Task OpenCustomerStockAsync()
    {
        IReadOnlyList<Customer> list = [];
        if (!await RunAsync(async () => list = await customers.GetAllAsync(includeInactive: false)))
        {
            return;
        }

        var name = await Dialogs.ChooseAsync("Bestand von Kunde", null, [.. list.Select(c => c.Name)]);
        if (list.FirstOrDefault(c => c.Name == name) is { } customer)
        {
            await navigation.GoToAsync(Routes.CustomerStock, customer.Id);
        }
    }
}
