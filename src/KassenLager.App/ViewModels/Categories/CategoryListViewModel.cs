using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Categories;

public sealed record CategoryRow(int Id, string Name, string Details, bool IsActive);

public sealed partial class CategoryListViewModel(
    CategoryService categories,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<CategoryListViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    [ObservableProperty]
    public partial IReadOnlyList<CategoryRow>? Items { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var all = await categories.GetAllAsync();
        Items = [.. all.Select(c => new CategoryRow(
            c.Id,
            c.Name,
            $"{TrackingTypeText.Of(c.TrackingType)} · {c.ArticleCount} Artikel",
            c.IsActive))];
    });

    [RelayCommand]
    private Task AddAsync() => navigation.GoToAsync(Routes.CategoryEdit);

    [RelayCommand]
    private Task OpenAsync(CategoryRow row) => navigation.GoToAsync(Routes.CategoryEdit, row.Id);
}
