using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Units;

/// <summary>Units are a plain name list: add, rename and delete via dialogs.</summary>
public sealed partial class UnitListViewModel(
    UnitService units,
    IDialogService dialogs,
    ILogger<UnitListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private const string Rename = "Umbenennen";
    private const string Delete = "Löschen";

    [ObservableProperty]
    public partial IReadOnlyList<UnitSummary>? Items { get; set; }

    public Task LoadAsync() => RunAsync(async () => Items = await units.GetAllAsync());

    [RelayCommand]
    private async Task AddAsync()
    {
        var name = await Dialogs.PromptAsync("Neue Einheit", "Bezeichnung der Einheit:", maxLength: Unit.NameMaxLength);
        if (name is not null && await RunAsync(() => units.SaveAsync(null, name)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task OpenAsync(UnitSummary unit)
    {
        var choice = await Dialogs.ChooseAsync(unit.Name, Delete, Rename);
        if (choice == Rename)
        {
            var name = await Dialogs.PromptAsync("Einheit umbenennen", "Neue Bezeichnung:", unit.Name, Unit.NameMaxLength);
            if (name is not null && await RunAsync(() => units.SaveAsync(unit.Id, name)))
            {
                await LoadAsync();
            }
        }
        else if (choice == Delete
            && await Dialogs.ConfirmAsync("Einheit löschen", $"„{unit.Name}“ wirklich löschen?", Delete)
            && await RunAsync(() => units.DeleteAsync(unit.Id)))
        {
            await LoadAsync();
        }
    }
}
