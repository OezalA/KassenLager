using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Articles;

public sealed record CategoryFilter(int? CategoryId, string Name);

/// <summary>
/// Article master data list. All articles are loaded once per appearance and filtered
/// in memory, so typing in the filter box needs no database round trip.
/// </summary>
public sealed partial class ArticleListViewModel(
    ArticleService articles,
    CategoryService categories,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<ArticleListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private static readonly CategoryFilter AllCategories = new(null, "Alle Kategorien");

    private IReadOnlyList<ArticleListItem> _all = [];

    [ObservableProperty]
    public partial IReadOnlyList<CategoryFilter>? CategoryFilters { get; set; }

    [ObservableProperty]
    public partial CategoryFilter? SelectedCategoryFilter { get; set; }

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ArticleListItem>? Items { get; set; }

    [ObservableProperty]
    public partial string? CountText { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var selectedCategoryId = SelectedCategoryFilter?.CategoryId;
        var categoryList = await categories.GetAllAsync();
        CategoryFilters = [AllCategories, .. categoryList.Select(c => new CategoryFilter(c.Id, c.Name))];
        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(f => f.CategoryId == selectedCategoryId) ?? AllCategories;

        _all = await articles.GetListAsync();
        ApplyFilter();
    });

    partial void OnSelectedCategoryFilterChanged(CategoryFilter? value) => ApplyFilter();

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    partial void OnShowInactiveChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private Task AddAsync() => navigation.GoToAsync(Routes.ArticleEdit);

    [RelayCommand]
    private Task OpenAsync(ArticleListItem article) => navigation.GoToAsync(Routes.ArticleDetail, article.Id);

    private void ApplyFilter()
    {
        var categoryId = SelectedCategoryFilter?.CategoryId;
        Items = [.. _all.Where(a =>
            (categoryId is null || a.CategoryId == categoryId)
            && (ShowInactive || a.IsActive)
            && a.Matches(FilterText))];
        CountText = Items.Count == 1 ? "1 Artikel" : $"{Items.Count} Artikel";
    }
}
