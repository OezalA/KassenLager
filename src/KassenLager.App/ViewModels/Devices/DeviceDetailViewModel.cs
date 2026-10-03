using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.App.ViewModels.Booking;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;
using Device = KassenLager.Core.Domain.Device;

namespace KassenLager.App.ViewModels.Devices;

/// <summary>A device with its complete history and the bookings possible in its current state.</summary>
public sealed partial class DeviceDetailViewModel(
    DeviceService devices,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<DeviceDetailViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIssue), nameof(IsInStore), nameof(CanReturnFromBranch), nameof(HasNote))]
    public partial DeviceDetail? Detail { get; set; }

    public bool CanIssue => Detail?.Device.IsAvailable == true;

    public bool IsInStore => Detail?.Device.IsInStore == true;

    public bool CanReturnFromBranch => Detail is { Device.IsVoided: false } detail && !detail.Device.IsInStore;

    public bool HasNote => !string.IsNullOrEmpty(Detail?.Note);

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _id = ReadId(query);

    public Task LoadAsync() => _id is not { } id ? Task.CompletedTask : RunAsync(async () => Detail = await devices.GetDetailAsync(id));

    [RelayCommand]
    private Task IssueAsync() => OpenFormAsync(Routes.BranchIssueBooking, null);

    [RelayCommand]
    private Task ReturnFromBranchAsync() => OpenFormAsync(Routes.BranchReturnBooking, null);

    [RelayCommand]
    private Task DeviceActionAsync(DeviceAction action) => OpenFormAsync(Routes.DeviceAction, action);

    [RelayCommand]
    private async Task EditNoteAsync()
    {
        if (_id is not { } id)
        {
            return;
        }

        var note = await Dialogs.PromptAsync("Notiz zum Gerät", "Notiz (leer = keine):", Detail?.Note, Device.NoteMaxLength);
        if (note is not null && await RunAsync(() => devices.SetNoteAsync(id, note)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private Task OpenArticleAsync() => Detail is { } detail
        ? navigation.GoToAsync(Routes.ArticleDetail, new Dictionary<string, object>
        {
            [Routes.IdParameter] = detail.Device.ArticleId,
            [Routes.CustomerIdParameter] = detail.Device.CustomerId,
        })
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenMovementAsync(MovementListItem movement) => navigation.GoToAsync(Routes.MovementDetail, movement.Id);

    private Task OpenFormAsync(string route, DeviceAction? action)
    {
        if (_id is not { } id)
        {
            return Task.CompletedTask;
        }

        var parameters = new Dictionary<string, object> { [Routes.DeviceIdParameter] = id };
        if (action is not null)
        {
            parameters[Routes.ActionParameter] = action.Value;
        }

        return navigation.GoToAsync(route, parameters);
    }
}
