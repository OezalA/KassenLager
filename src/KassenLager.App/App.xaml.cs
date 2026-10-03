using KassenLager.App.Services;
using KassenLager.App.Views;
using KassenLager.Data;
using Microsoft.Extensions.Logging;

namespace KassenLager.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ILogger<App> _logger;

    public App(IServiceProvider services, ThemeService themeService, ILogger<App> logger)
    {
        InitializeComponent();
        _services = services;
        _logger = logger;
        themeService.ApplySavedTheme(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page root;
        try
        {
            _services.GetRequiredService<DatabaseInitializer>().Initialize();
            root = _services.GetRequiredService<AppShell>();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database initialization failed");
            root = new StartupErrorPage();
        }

        return new Window(root);
    }
}
