namespace KassenLager.App.Views;

/// <summary>Shown instead of the shell when the database cannot be opened or migrated.</summary>
public sealed class StartupErrorPage : ContentPage
{
    public StartupErrorPage()
    {
        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Die Datenbank konnte nicht geöffnet werden.", FontSize = 20, FontFamily = "OpenSansSemibold" },
                new Label { Text = "Bitte die App schließen und neu starten. Die technischen Details wurden im Protokoll gespeichert." },
            },
        };
    }
}
