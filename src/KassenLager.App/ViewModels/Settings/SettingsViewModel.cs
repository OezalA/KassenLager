using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Settings;

public sealed record ThemeChoice(ThemeOption Option, string Name);

public sealed partial class SettingsViewModel(
    SettingsService settings,
    ThemeService themeService,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<SettingsViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable
{
    private bool _isLoaded;

    public IReadOnlyList<ThemeChoice> ThemeChoices { get; } =
    [
        new(ThemeOption.System, "Wie System"),
        new(ThemeOption.Light, "Hell"),
        new(ThemeOption.Dark, "Dunkel"),
    ];

    public int UserNameMaxLength => SettingsService.UserNameMaxLength;

    public string AppVersion => $"Version {AppInfo.Current.VersionString} (Build {AppInfo.Current.BuildString})";

    [ObservableProperty]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    public partial ThemeChoice? SelectedTheme { get; set; }

    public Task LoadAsync() => RunAsync(async () =>
    {
        UserName = await settings.GetUserNameAsync();
        SelectedTheme = ThemeChoices.First(c => c.Option == themeService.Current);
        _isLoaded = true;
    });

    partial void OnSelectedThemeChanged(ThemeChoice? value)
    {
        if (_isLoaded && value is not null)
        {
            themeService.SetTheme(value.Option);
        }
    }

    [RelayCommand]
    private Task SaveUserNameAsync() => RunAsync(async () =>
    {
        await settings.SetUserNameAsync(UserName);
        UserName = await settings.GetUserNameAsync();
        await Dialogs.ToastAsync("Benutzername gespeichert");
    });

    [RelayCommand]
    private Task OpenCustomersAsync() => navigation.GoToAsync(Routes.Customers);

    [RelayCommand]
    private Task OpenCategoriesAsync() => navigation.GoToAsync(Routes.Categories);

    [RelayCommand]
    private Task OpenUnitsAsync() => navigation.GoToAsync(Routes.Units);
}
