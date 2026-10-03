using CommunityToolkit.Mvvm.ComponentModel;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Booking;

public sealed record StateChoice(DeviceState State, string Name);

/// <summary>
/// Common part of the booking forms: customer (remembered per device), date, reference and note.
/// Forms load once, so returning from a picker page keeps the entries.
/// </summary>
public abstract partial class BookingFormViewModel(
    CustomerService customers,
    UserPreferences preferences,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private bool _isLoaded;

    protected int? PresetCustomerId { get; private set; }

    protected int? PresetDeviceId { get; private set; }

    protected INavigationService Navigation { get; } = navigation;

    public int ReferenceMaxLength => Movement.ReferenceMaxLength;

    public int NoteMaxLength => Movement.NoteMaxLength;

    public DateTime MaximumDate => DateTime.Today;

    [ObservableProperty]
    public partial IReadOnlyList<Customer>? Customers { get; set; }

    [ObservableProperty]
    public partial Customer? SelectedCustomer { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial string? Reference { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    protected DateOnly? BookingDate => Date is { } date ? DateOnly.FromDateTime(date) : null;

    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        PresetCustomerId = query.TryGetValue(Routes.CustomerIdParameter, out var customer) && customer is int customerId ? customerId : null;
        PresetDeviceId = query.TryGetValue(Routes.DeviceIdParameter, out var device) && device is int deviceId ? deviceId : null;
    }

    public Task LoadAsync() => _isLoaded ? Task.CompletedTask : RunAsync(async () =>
    {
        Customers = await customers.GetAllAsync(includeInactive: false);
        var preferredId = PresetCustomerId ?? preferences.BookingCustomerId;
        Date = DateTime.Today;
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == preferredId) ?? Customers.FirstOrDefault();
        await OnFirstLoadAsync();
        _isLoaded = true;
    });

    /// <summary>Selects the customer of a preset device, even if that customer is inactive.</summary>
    protected async Task SelectCustomerAsync(int customerId)
    {
        if (Customers?.FirstOrDefault(c => c.Id == customerId) is not { } customer)
        {
            customer = await customers.GetAsync(customerId);
            Customers = [.. Customers ?? [], customer];
        }

        SelectedCustomer = customer;
    }

    protected virtual Task OnFirstLoadAsync() => Task.CompletedTask;

    protected virtual void OnCustomerChanged(Customer? customer)
    {
    }

    partial void OnSelectedCustomerChanged(Customer? value) => OnCustomerChanged(value);

    /// <summary>Runs the booking; on success remembers the customer, confirms and returns to the previous page.</summary>
    protected async Task BookAsync(Func<Task> booking, string successMessage)
    {
        if (await RunAsync(booking))
        {
            preferences.BookingCustomerId = SelectedCustomer?.Id;
            await Dialogs.ToastAsync(successMessage);
            await Navigation.GoBackAsync();
        }
    }
}
