using System.Globalization;
using CommunityToolkit.Maui;
using KassenLager.App.Services;
using KassenLager.App.Services.Logging;
using KassenLager.App.ViewModels;
using KassenLager.App.ViewModels.Articles;
using KassenLager.App.ViewModels.Categories;
using KassenLager.App.ViewModels.Customers;
using KassenLager.App.ViewModels.Settings;
using KassenLager.App.ViewModels.Units;
using KassenLager.App.Views;
using KassenLager.App.Views.Articles;
using KassenLager.App.Views.Categories;
using KassenLager.App.Views.Customers;
using KassenLager.App.Views.Settings;
using KassenLager.App.Views.Units;
using KassenLager.Core;
using KassenLager.Data;
using Microsoft.Extensions.Logging;

namespace KassenLager.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // German-only UI: dd.MM.yyyy, decimal comma.
        var culture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(FileSystem.AppDataDirectory, "logs")));
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddKassenLagerData(Path.Combine(FileSystem.AppDataDirectory, "kassenlager.db"));
        builder.Services.AddKassenLagerCore();

        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services
            .AddPage<OverviewPage, OverviewViewModel>()
            .AddPage<SearchPage>()
            .AddPage<BookingPage>()
            .AddPage<InventoryPage>()
            .AddPage<MorePage, MoreViewModel>()
            .AddPage<SettingsPage, SettingsViewModel>()
            .AddPage<CustomerListPage, CustomerListViewModel>()
            .AddPage<CustomerEditPage, CustomerEditViewModel>()
            .AddPage<CategoryListPage, CategoryListViewModel>()
            .AddPage<CategoryEditPage, CategoryEditViewModel>()
            .AddPage<UnitListPage, UnitListViewModel>()
            .AddPage<ArticleListPage, ArticleListViewModel>()
            .AddPage<ArticleEditPage, ArticleEditViewModel>();

        var app = builder.Build();
        RegisterGlobalExceptionLogging(app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Unhandled"));
        return app;
    }

    private static IServiceCollection AddPage<TPage>(this IServiceCollection services)
        where TPage : Page =>
        services.AddTransient<TPage>();

    private static IServiceCollection AddPage<TPage, TViewModel>(this IServiceCollection services)
        where TPage : Page
        where TViewModel : class =>
        services.AddTransient<TPage>().AddTransient<TViewModel>();

    private static void RegisterGlobalExceptionLogging(ILogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }
}
