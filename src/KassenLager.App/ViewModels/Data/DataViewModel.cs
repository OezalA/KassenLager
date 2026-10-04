using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using KassenLager.Data;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Data;

public enum ExportKind
{
    Journal = 1,
    BranchIssues = 2,
}

/// <summary>Import, exports and the full backup (create, share, restore).</summary>
public sealed partial class DataViewModel(
    ExportService exports,
    DatabaseBackupService backups,
    IFileService files,
    UserPreferences preferences,
    TimeProvider clock,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<DataViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    public const int BackupReminderDays = 30;

    [ObservableProperty]
    public partial string? LastBackupText { get; set; }

    public Task LoadAsync()
    {
        LastBackupText = DescribeLastBackup(preferences.LastBackupAt, clock);
        return Task.CompletedTask;
    }

    /// <summary>"Letzte Sicherung: 04.10.2026 (vor 3 Tagen)" or "Noch keine Sicherung".</summary>
    public static string DescribeLastBackup(DateTime? lastBackupAt, TimeProvider clock)
    {
        if (lastBackupAt is not { } at)
        {
            return "Noch keine Sicherung erstellt";
        }

        var days = AppTime.Today(clock).DayNumber - AppTime.ToLocalDate(at).DayNumber;
        var ago = days switch
        {
            0 => "heute",
            1 => "gestern",
            _ => $"vor {days} Tagen",
        };
        return $"Letzte Sicherung: {AppTime.ToLocal(at):dd.MM.yyyy} ({ago})";
    }

    [RelayCommand]
    private Task OpenImportAsync() => navigation.GoToAsync(Routes.Import);

    [RelayCommand]
    private Task OpenExportAsync(ExportKind kind) =>
        navigation.GoToAsync(Routes.Export, new Dictionary<string, object> { [Routes.KindParameter] = kind });

    [RelayCommand]
    private Task ShareTemplateAsync() => ShareExcelAsync("Vorlage", "Importvorlage", exports.WriteTemplateAsync);

    [RelayCommand]
    private Task ExportStockAsync() => ShareExcelAsync("Gesamtbestand", "Gesamtbestand", exports.WriteStockAsync);

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        string? path = null;
        if (!await RunAsync(async () =>
            {
                files.ClearExports();
                path = await backups.CreateBackupAsync(files.ExportDirectory);
            }))
        {
            return;
        }

        // Only a backup that left the app counts for the reminder.
        var delivered = false;
        if (await RunAsync(async () => delivered = await files.SaveOrShareAsync(path!, "Datensicherung")) && delivered)
        {
            preferences.LastBackupAt = clock.GetUtcNow().UtcDateTime;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        PickedFile? file = null;
        BackupInfo? info = null;
        if (!await RunAsync(async () =>
            {
                file = await files.PickAsync(PickFileKind.Backup);
                if (file is not null)
                {
                    info = await backups.InspectAsync(file.LocalPath);
                }
            })
            || file is null || info is null)
        {
            return;
        }

        var lastMovement = info.LastMovementLocalTime is { } at ? $", letzte Buchung am {at:dd.MM.yyyy}" : string.Empty;
        if (!await Dialogs.ConfirmAsync(
                "Sicherung wiederherstellen",
                $"„{file.FileName}“ enthält {info.Articles} Artikel, {info.Devices} Geräte und {info.Movements} Buchungen{lastMovement}.\n\n"
                + "Alle aktuellen Daten werden dadurch ersetzt. Vorher wird automatisch eine Sicherung der aktuellen Daten auf dem Gerät angelegt.",
                "Wiederherstellen"))
        {
            return;
        }

        if (await RunAsync(() => backups.RestoreAsync(file.LocalPath, files.AutomaticBackupDirectory)))
        {
            await Dialogs.ToastAsync("Sicherung wiederhergestellt");
            await navigation.GoToTabAsync(Routes.Overview);
        }
    }

    private async Task ShareExcelAsync(string prefix, string title, Func<Stream, CancellationToken, Task> write)
    {
        string? path = null;
        if (await RunAsync(async () => path = await files.CreateExportAsync(exports.FileName(prefix), stream => write(stream, default))))
        {
            await RunAsync(() => files.SaveOrShareAsync(path!, title));
        }
    }
}
