using System.Globalization;
using CommunityToolkit.Maui;
using KassenLager.App.Services;
using KassenLager.App.Services.Logging;
using KassenLager.App.ViewModels;
using KassenLager.App.ViewModels.Articles;
using KassenLager.App.ViewModels.Booking;
using KassenLager.App.ViewModels.BranchIssues;
using KassenLager.App.ViewModels.Categories;
using KassenLager.App.ViewModels.Customers;
using KassenLager.App.ViewModels.Data;
using KassenLager.App.ViewModels.Devices;
using KassenLager.App.ViewModels.Movements;
using KassenLager.App.ViewModels.Pickers;
using KassenLager.App.ViewModels.Settings;
using KassenLager.App.ViewModels.Stock;
using KassenLager.App.ViewModels.Units;
using KassenLager.App.Views;
using KassenLager.App.Views.Articles;
using KassenLager.App.Views.Booking;
using KassenLager.App.Views.BranchIssues;
using KassenLager.App.Views.Categories;
using KassenLager.App.Views.Customers;
using KassenLager.App.Views.Data;
using KassenLager.App.Views.Devices;
using KassenLager.App.Views.Movements;
using KassenLager.App.Views.Pickers;
using KassenLager.App.Views.Settings;
using KassenLager.App.Views.Stock;
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
        builder.Services.AddSingleton<UserPreferences>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IPickerService, PickerService>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services
            .AddPage<OverviewPage, OverviewViewModel>()
            .AddPage<SearchPage, SearchViewModel>()
            .AddPage<BookingPage, BookingViewModel>()
            .AddPage<InventoryPage>()
            .AddPage<MorePage, MoreViewModel>()
            .AddPage<SettingsPage, SettingsViewModel>()
            .AddPage<CustomerListPage, CustomerListViewModel>()
            .AddPage<CustomerEditPage, CustomerEditViewModel>()
            .AddPage<CustomerStockPage, CustomerStockViewModel>()
            .AddPage<CategoryListPage, CategoryListViewModel>()
            .AddPage<CategoryEditPage, CategoryEditViewModel>()
            .AddPage<UnitListPage, UnitListViewModel>()
            .AddPage<ArticleListPage, ArticleListViewModel>()
            .AddPage<ArticleDetailPage, ArticleDetailViewModel>()
            .AddPage<ArticleEditPage, ArticleEditViewModel>()
            .AddPage<DeviceListPage, DeviceListViewModel>()
            .AddPage<DeviceDetailPage, DeviceDetailViewModel>()
            .AddPage<BranchIssueListPage, BranchIssueListViewModel>()
            .AddPage<BranchIssueDetailPage, BranchIssueDetailViewModel>()
            .AddPage<MovementListPage, MovementListViewModel>()
            .AddPage<MovementDetailPage, MovementDetailViewModel>()
            .AddPage<DataPage, DataViewModel>()
            .AddPage<ImportPage, ImportViewModel>()
            .AddPage<ExportPage, ExportViewModel>()
            .AddPage<GoodsReceiptPage, GoodsReceiptViewModel>()
            .AddPage<ConsumptionPage, ConsumptionViewModel>()
            .AddPage<BranchIssueFormPage, BranchIssueFormViewModel>()
            .AddPage<BranchReturnFormPage, BranchReturnFormViewModel>()
            .AddPage<DeviceActionPage, DeviceActionViewModel>()
            .AddPage<ArticlePickerPage, ArticlePickerViewModel>()
            .AddPage<DevicePickerPage, DevicePickerViewModel>();

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
