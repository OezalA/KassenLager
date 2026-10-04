using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Data;

/// <summary>Bewegungsjournal or Ausgaben-Liste as Excel, with date range and customer filter.</summary>
public sealed partial class ExportViewModel(
    ExportService exports,
    CustomerService customers,
    IFileService files,
    IDialogService dialogs,
    ILogger<ExportViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private ExportKind _kind = ExportKind.Journal;
    private bool _isLoaded;

    public DateTime MaximumDate => DateTime.Today;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CustomerFilter>? CustomerFilters { get; set; }

    [ObservableProperty]
    public partial CustomerFilter? SelectedCustomerFilter { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.KindParameter, out var value) && value is ExportKind kind)
        {
            _kind = kind;
        }

        (Title, Description) = _kind == ExportKind.BranchIssues
            ? ("Ausgaben-Liste", "An Filialen ausgegebene Geräte mit Kundengerät, Ticket und Rücknahme.")
            : ("Bewegungsjournal", "Alle Buchungen mit Datum, Buchungsart, Menge, Zustand, Filiale und Referenz.");
    }

    public Task LoadAsync() => _isLoaded ? Task.CompletedTask : RunAsync(async () =>
    {
        CustomerFilters = CustomerFilter.Build(await customers.GetAllAsync());
        SelectedCustomerFilter = CustomerFilter.All;
        FromDate = new DateTime(DateTime.Today.Year, 1, 1);
        ToDate = DateTime.Today;
        _isLoaded = true;
    });

    [RelayCommand]
    private async Task ExportAsync()
    {
        var from = DateOnly.FromDateTime(FromDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(ToDate ?? DateTime.Today);
        var customerId = SelectedCustomerFilter?.CustomerId;
        var prefix = _kind == ExportKind.BranchIssues ? "Ausgaben" : "Bewegungsjournal";

        string? path = null;
        var created = await RunAsync(async () => path = await files.CreateExportAsync(
            exports.FileName(prefix),
            stream => _kind == ExportKind.BranchIssues
                ? exports.WriteBranchIssuesAsync(stream, from, to, customerId)
                : exports.WriteJournalAsync(stream, from, to, customerId)));

        if (created)
        {
            await files.ShareAsync(path!, $"{Title} teilen");
        }
    }
}
