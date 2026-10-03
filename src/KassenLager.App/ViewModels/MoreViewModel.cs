using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;

namespace KassenLager.App.ViewModels;

public sealed partial class MoreViewModel(INavigationService navigation)
{
    [RelayCommand]
    private Task OpenArticlesAsync() => navigation.GoToAsync(Routes.Articles);

    [RelayCommand]
    private Task OpenSettingsAsync() => navigation.GoToAsync(Routes.Settings);
}
