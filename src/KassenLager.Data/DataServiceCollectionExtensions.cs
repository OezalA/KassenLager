using KassenLager.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KassenLager.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddKassenLagerData(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<KassenLagerDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<IAppDbContextFactory, AppDbContextFactory>();
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }
}

public sealed class AppDbContextFactory(IDbContextFactory<KassenLagerDbContext> inner) : IAppDbContextFactory
{
    public IAppDbContext CreateDbContext() => inner.CreateDbContext();
}
