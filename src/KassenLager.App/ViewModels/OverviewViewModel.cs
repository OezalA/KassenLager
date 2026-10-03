using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels;

/// <summary>Dashboard. Phase 1 shows master data counts; stock figures follow in Phase 2.</summary>
public sealed partial class OverviewViewModel(
    CustomerService customers,
    CategoryService categories,
    ArticleService articles,
    SettingsService settings,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OverviewViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    [ObservableProperty]
    public partial string? Greeting { get; set; }

    [ObservableProperty]
    public partial bool IsUserNameMissing { get; set; }

    [ObservableProperty]
    public partial int CustomerCount { get; set; }

    [ObservableProperty]
    public partial int CategoryCount { get; set; }

    [ObservableProperty]
    public partial int ArticleCount { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var userName = await settings.GetUserNameAsync();
        Greeting = userName is null ? "Willkommen" : $"Hallo {userName}";
        IsUserNameMissing = userName is null;

        CustomerCount = (await customers.GetAllAsync(includeInactive: false)).Count;
        CategoryCount = (await categories.GetAllAsync(includeInactive: false)).Count;
        ArticleCount = (await articles.GetListAsync(includeInactive: false)).Count;
    });

    [RelayCommand]
    private Task OpenArticlesAsync() => navigation.GoToAsync(Routes.Articles);

    [RelayCommand]
    private Task OpenSettingsAsync() => navigation.GoToAsync(Routes.Settings);
}
