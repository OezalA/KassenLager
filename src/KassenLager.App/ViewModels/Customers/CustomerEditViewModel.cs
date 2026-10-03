using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Customers;

public sealed partial class CustomerEditViewModel(
    CustomerService customers,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<CustomerEditViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;
    private bool _isLoaded;

    public int NameMaxLength => Customer.NameMaxLength;

    public int ShortNameMaxLength => Customer.ShortNameMaxLength;

    public int NoteMaxLength => Customer.NoteMaxLength;

    public bool IsExisting => _id is not null;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? ShortName { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _id = ReadId(query);
        OnPropertyChanged(nameof(IsExisting));
    }

    // Loads once; returning to the page must not overwrite unsaved edits.
    public Task LoadAsync() => _isLoaded ? Task.CompletedTask : RunAsync(async () =>
    {
        if (_id is null)
        {
            Title = "Neuer Kunde";
            IsActive = true;
        }
        else
        {
            var customer = await customers.GetAsync(_id.Value);
            Title = "Kunde bearbeiten";
            Name = customer.Name;
            ShortName = customer.ShortName;
            Note = customer.Note;
            IsActive = customer.IsActive;
        }

        _isLoaded = true;
    });

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (await RunAsync(() => customers.SaveAsync(_id, new CustomerInput(Name, ShortName, Note, IsActive))))
        {
            await Dialogs.ToastAsync("Kunde gespeichert");
            await navigation.GoBackAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is not { } id
            || !await Dialogs.ConfirmAsync("Kunde löschen", $"„{Name}“ wirklich löschen?", "Löschen"))
        {
            return;
        }

        if (await RunAsync(() => customers.DeleteAsync(id)))
        {
            await Dialogs.ToastAsync("Kunde gelöscht");
            await navigation.GoBackAsync();
        }
    }
}
