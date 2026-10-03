using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Movements;

/// <summary>One movement with its links (device, branch issue, storno) and the storno action.</summary>
public sealed partial class MovementDetailViewModel(
    JournalService journal,
    ReversalService reversals,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<MovementDetailViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReversalInfo), nameof(ShowBlocker))]
    public partial MovementDetail? Detail { get; set; }

    /// <summary>"Storniert am …" or "Storno der Buchung … vom …".</summary>
    public string? ReversalInfo => Detail switch
    {
        { ReversedAt: { } at } => $"Storniert am {AppTime.ToLocal(at):dd.MM.yyyy HH:mm}",
        { ReversalOfType: { } type, ReversalOfOccurredAt: { } at } => $"Storno von „{Labels.Of(type)}“ vom {AppTime.ToLocal(at):dd.MM.yyyy HH:mm}",
        _ => null,
    };

    /// <summary>Why storno is not possible; hidden when the reason is obvious (storno or already reversed).</summary>
    public bool ShowBlocker => Detail is { CanReverse: false, ReversedById: null } detail && detail.Movement.Type != MovementType.Reversal;

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _id = ReadId(query);

    public Task LoadAsync() => _id is not { } id ? Task.CompletedTask : RunAsync(async () => Detail = await journal.GetDetailAsync(id));

    [RelayCommand]
    private async Task ReverseAsync()
    {
        if (Detail is not { CanReverse: true } detail)
        {
            return;
        }

        var reason = await Dialogs.PromptAsync(
            "Buchung stornieren",
            $"„{detail.Movement.TypeName}“ wird durch eine Gegenbuchung aufgehoben. Grund (optional):",
            maxLength: Movement.NoteMaxLength);
        if (reason is null)
        {
            return;
        }

        if (await RunAsync(() => reversals.ReverseAsync(detail.Movement.Id, reason)))
        {
            await Dialogs.ToastAsync("Buchung storniert");
            await LoadAsync();
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync() =>
        Detail?.Movement.DeviceId is { } deviceId ? navigation.GoToAsync(Routes.DeviceDetail, deviceId) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenArticleAsync() =>
        Detail is { } detail
            ? navigation.GoToAsync(Routes.ArticleDetail, new Dictionary<string, object>
            {
                [Routes.IdParameter] = detail.Movement.ArticleId,
                [Routes.CustomerIdParameter] = detail.Movement.CustomerId,
            })
            : Task.CompletedTask;

    [RelayCommand]
    private Task OpenBranchIssueAsync() =>
        Detail?.BranchIssueId is { } issueId ? navigation.GoToAsync(Routes.BranchIssueDetail, issueId) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenLinkedAsync() =>
        (Detail?.ReversedById ?? Detail?.Movement.ReversalOfId) is { } linkedId
            ? navigation.GoToAsync(Routes.MovementDetail, linkedId)
            : Task.CompletedTask;
}
