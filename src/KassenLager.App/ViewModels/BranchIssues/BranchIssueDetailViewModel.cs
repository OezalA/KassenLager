using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.BranchIssues;

/// <summary>One issue to a branch; only the date the customer's device was sent can be changed.</summary>
public sealed partial class BranchIssueDetailViewModel(
    BranchService branches,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<BranchIssueDetailViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;

    [ObservableProperty]
    public partial BranchIssueListItem? Issue { get; set; }

    [ObservableProperty]
    public partial bool IsSent { get; set; }

    [ObservableProperty]
    public partial DateTime? SentDate { get; set; }

    public DateTime MaximumDate => DateTime.Today;

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _id = ReadId(query);

    public Task LoadAsync() => _id is not { } id ? Task.CompletedTask : RunAsync(async () =>
    {
        Issue = await branches.GetAsync(id);
        IsSent = Issue.CustomerDeviceSentOn is not null;
        SentDate = Issue.CustomerDeviceSentOn?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
    });

    [RelayCommand]
    private async Task SaveSentDateAsync()
    {
        if (_id is not { } id)
        {
            return;
        }

        DateOnly? sentOn = IsSent && SentDate is { } date ? DateOnly.FromDateTime(date) : null;
        if (await RunAsync(() => branches.SetCustomerDeviceSentOnAsync(id, sentOn)))
        {
            await Dialogs.ToastAsync("Gespeichert");
            await LoadAsync();
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync() => Issue is { } issue ? navigation.GoToAsync(Routes.DeviceDetail, issue.DeviceId) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenMovementAsync() => Issue is { } issue ? navigation.GoToAsync(Routes.MovementDetail, issue.MovementId) : Task.CompletedTask;
}
