namespace KassenLager.App.Services;

public enum ThemeOption
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Light/dark theme choice; a device preference, not part of the data backup.</summary>
public sealed class ThemeService(IPreferences preferences)
{
    private const string PreferenceKey = "theme";

    public ThemeOption Current => (ThemeOption)preferences.Get(PreferenceKey, (int)ThemeOption.System);

    public void ApplySavedTheme(Application application) => application.UserAppTheme = ToAppTheme(Current);

    public void SetTheme(ThemeOption option)
    {
        preferences.Set(PreferenceKey, (int)option);
        if (Application.Current is { } application)
        {
            application.UserAppTheme = ToAppTheme(option);
        }
    }

    private static AppTheme ToAppTheme(ThemeOption option) => option switch
    {
        ThemeOption.Light => AppTheme.Light,
        ThemeOption.Dark => AppTheme.Dark,
        _ => AppTheme.Unspecified,
    };
}
