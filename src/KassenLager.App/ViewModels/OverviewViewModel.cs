using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.App.ViewModels.Data;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels;

/// <summary>Dashboard: stock per customer, warnings, quick actions and the latest bookings.</summary>
public sealed partial class OverviewViewModel(
    StockService stock,
    JournalService journal,
    SettingsService settings,
    UserPreferences preferences,
    TimeProvider clock,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OverviewViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private const int RecentCount = 5;

    [ObservableProperty]
    public partial string? Greeting { get; set; }

    [ObservableProperty]
    public partial bool IsUserNameMissing { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerStockSummary>? Customers { get; set; }

    [ObservableProperty]
    public partial int DefectiveCount { get; set; }

    [ObservableProperty]
    public partial int BelowMinimumCount { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MovementListItem>? RecentMovements { get; set; }

    [ObservableProperty]
    public partial bool HasMovements { get; set; }

    [ObservableProperty]
    public partial bool ShowBackupReminder { get; set; }

    [ObservableProperty]
    public partial string? BackupReminderText { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var userName = await settings.GetUserNameAsync();
        Greeting = userName is null ? "Willkommen" : $"Hallo {userName}";
        IsUserNameMissing = userName is null;

        Customers = await stock.GetCustomerSummariesAsync();
        DefectiveCount = Customers.Sum(c => c.DefectiveDevices);
        BelowMinimumCount = Customers.Sum(c => c.BelowMinimum);

        RecentMovements = await journal.GetPageAsync(new MovementFilter(), 0, RecentCount);
        HasMovements = RecentMovements.Count > 0;

        var lastBackup = preferences.LastBackupAt;
        ShowBackupReminder = lastBackup is null || clock.GetUtcNow().UtcDateTime - lastBackup.Value > TimeSpan.FromDays(DataViewModel.BackupReminderDays);
        BackupReminderText = DataViewModel.DescribeLastBackup(lastBackup, clock);
    });

    [RelayCommand]
    private Task OpenSearchAsync() => navigation.GoToTabAsync(Routes.Search);

    [RelayCommand]
    private Task OpenRouteAsync(string route) => navigation.GoToAsync(route);

    [RelayCommand]
    private Task OpenCustomerAsync(CustomerStockSummary summary) => navigation.GoToAsync(Routes.CustomerStock, summary.CustomerId);

    [RelayCommand]
    private Task OpenMovementAsync(MovementListItem movement) => navigation.GoToAsync(Routes.MovementDetail, movement.Id);

    [RelayCommand]
    private Task OpenSettingsAsync() => navigation.GoToAsync(Routes.Settings);
}
