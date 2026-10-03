using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KassenLager.Core.Abstractions;

/// <summary>
/// Persistence abstraction used by the Core services; implemented by the Data layer.
/// Contexts are short-lived: create one per operation via <see cref="IAppDbContextFactory"/>.
/// </summary>
public interface IAppDbContext : IDisposable, IAsyncDisposable
{
    DbSet<Customer> Customers { get; }

    DbSet<Category> Categories { get; }

    DbSet<Unit> Units { get; }

    DbSet<Article> Articles { get; }

    DbSet<AppSetting> AppSettings { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAppDbContextFactory
{
    IAppDbContext CreateDbContext();
}
