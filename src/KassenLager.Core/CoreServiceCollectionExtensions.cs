using KassenLager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KassenLager.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers the business services. They are stateless and create a DbContext per call.</summary>
    public static IServiceCollection AddKassenLagerCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<CustomerService>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<UnitService>();
        services.AddSingleton<ArticleService>();
        services.AddSingleton<SettingsService>();

        services.AddSingleton<StockService>();
        services.AddSingleton<BookingService>();
        services.AddSingleton<BranchService>();
        services.AddSingleton<ReversalService>();
        services.AddSingleton<DeviceService>();
        services.AddSingleton<JournalService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<OrderService>();
        return services;
    }
}
