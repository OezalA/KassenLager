using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Pickers;

public sealed record ArticlePickerRow(ArticleListItem Article, string? StockText);

/// <summary>Chooses an active article, optionally with the stock of the form's customer.</summary>
public sealed partial class ArticlePickerViewModel(
    ArticleService articles,
    StockService stock,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<ArticlePickerViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private PickerRequest<ArticlePickerOptions, ArticleListItem>? _request;
    private IReadOnlyList<ArticlePickerRow> _all = [];
    private bool _isLoaded;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ArticlePickerRow>? Items { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _request = query.TryGetValue(Routes.RequestParameter, out var value)
            ? value as PickerRequest<ArticlePickerOptions, ArticleListItem>
            : null;
        Title = _request?.Options.Title;
    }

    public Task LoadAsync() => _isLoaded || _request is null ? Task.CompletedTask : RunAsync(async () =>
    {
        var options = _request.Options;
        var list = await articles.GetListAsync(includeInactive: false);
        var lines = options.CustomerId is { } customerId ? await stock.GetLinesAsync(customerId) : [];

        _all = [.. list
            .Where(a => options.TrackingType is null || a.TrackingType == options.TrackingType)
            .Select(a => new ArticlePickerRow(
                a,
                options.CustomerId is null ? null : lines.FirstOrDefault(l => l.ArticleId == a.Id)?.StockText ?? "kein Bestand"))];
        ApplyFilter();
        _isLoaded = true;
    });

    /// <summary>Called when the page disappears; leaving without a choice returns <c>null</c>.</summary>
    public void Cancel() => _request?.Complete(null);

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private async Task SelectAsync(ArticlePickerRow row)
    {
        if (_request is null || _request.IsCompleted)
        {
            return;
        }

        _request.Complete(row.Article);
        await navigation.GoBackAsync();
    }

    private void ApplyFilter() => Items = [.. _all.Where(r => r.Article.Matches(FilterText))];
}
