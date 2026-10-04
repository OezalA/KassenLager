using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Data;

/// <summary>
/// Excel import: pick file → read → validate → preview → confirm → import in one transaction.
/// The preview is recalculated when the stock mode changes.
/// </summary>
public sealed partial class ImportViewModel(
    ImportService imports,
    ExportService exports,
    IFileService files,
    IDialogService dialogs,
    ILogger<ImportViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private ExcelWorkbook? _workbook;
    private ImportResult? _result;
    private bool _isLoaded;

    [ObservableProperty]
    public partial string? FileName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mode))]
    public partial bool IsSetMode { get; set; }

    [ObservableProperty]
    public partial bool IsAddMode { get; set; }

    [ObservableProperty]
    public partial bool ErrorsOnly { get; set; }

    [ObservableProperty]
    public partial bool HasPreview { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial bool CanImport { get; set; }

    [ObservableProperty]
    public partial string? ImportButtonText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ImportRowResult>? Rows { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ImportLogItem>? Log { get; set; }

    [ObservableProperty]
    public partial bool HasLog { get; set; }

    public ImportMode Mode => IsAddMode ? ImportMode.Add : ImportMode.Set;

    public Task LoadAsync() => RunAsync(async () =>
    {
        if (!_isLoaded)
        {
            IsSetMode = true;
            _isLoaded = true;
        }

        Log = await imports.GetLogAsync();
        HasLog = Log.Count > 0;
    });

    partial void OnIsAddModeChanged(bool value) => _ = PreviewAsync();

    partial void OnErrorsOnlyChanged(bool value) => ShowRows();

    [RelayCommand]
    private async Task PickFileAsync()
    {
        PickedFile? file = null;
        var read = await RunAsync(async () =>
        {
            file = await files.PickAsync(PickFileKind.Excel);
            if (file is not null)
            {
                await using var stream = File.OpenRead(file.LocalPath);
                _workbook = ExcelWorkbook.Read(stream);
            }
        });

        if (read && file is not null)
        {
            FileName = file.FileName;
            IsDone = false;
            await PreviewAsync();
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (_workbook is not { } workbook || _result is not { HasChanges: true } preview || FileName is not { } fileName)
        {
            return;
        }

        var question = preview.HasErrors
            ? $"{preview.NewCount + preview.UpdatedCount} Zeilen werden übernommen, {preview.ErrorCount} fehlerhafte Zeilen übersprungen."
            : $"{preview.NewCount + preview.UpdatedCount} Zeilen werden übernommen.";
        if (!await Dialogs.ConfirmAsync("Import ausführen", question, "Importieren"))
        {
            return;
        }

        ImportResult? result = null;
        if (await RunAsync(async () => result = await imports.ImportAsync(workbook, Mode, fileName)))
        {
            _workbook = null;
            _result = result;
            IsDone = true;
            CanImport = false;
            SummaryText = $"Import abgeschlossen: {Summary(result!)}";
            ShowRows();
            await Dialogs.ToastAsync("Import abgeschlossen");
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ShareTemplateAsync()
    {
        string? path = null;
        if (await RunAsync(async () => path = await files.CreateExportAsync(exports.FileName("Vorlage"), s => exports.WriteTemplateAsync(s))))
        {
            await RunAsync(() => files.SaveOrShareAsync(path!, "Importvorlage"));
        }
    }

    private async Task PreviewAsync()
    {
        if (_workbook is not { } workbook)
        {
            return;
        }

        ImportResult? result = null;
        if (await RunAsync(async () => result = await imports.PreviewAsync(workbook, Mode)))
        {
            _result = result;
            HasPreview = true;
            CanImport = result!.HasChanges;
            ImportButtonText = result.HasErrors ? "Nur gültige Zeilen importieren" : "Importieren";
            SummaryText = workbook.RowCount == 0 ? "Die Datei enthält keine Datenzeilen." : $"Vorschau: {Summary(result)}";
            ShowRows();
        }
    }

    private void ShowRows() =>
        Rows = _result is null ? [] : [.. _result.Rows.Where(r => !ErrorsOnly || r.IsError)];

    private static string Summary(ImportResult result) =>
        $"{result.NewCount} neu · {result.UpdatedCount} wird aktualisiert · {result.UnchangedCount} unverändert · {result.ErrorCount} fehlerhaft";
}
