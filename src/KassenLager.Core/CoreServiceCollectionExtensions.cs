using KassenLager.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KassenLager.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers the business services. They are stateless and create a DbContext per call.</summary>
    public static IServiceCollection AddKassenLagerCore(this IServiceCollection services)
    {
        services.AddSingleton<CustomerService>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<UnitService>();
        services.AddSingleton<ArticleService>();
        services.AddSingleton<SettingsService>();
        return services;
    }
}
