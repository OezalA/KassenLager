using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Data;

public sealed class KassenLagerDbContext(DbContextOptions<KassenLagerDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RefreshNormalizedKeys();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RefreshNormalizedKeys();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit registration instead of assembly scanning keeps the model trim-friendly.
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new ArticleConfiguration());
        modelBuilder.ApplyConfiguration(new AppSettingConfiguration());
    }

    private void RefreshNormalizedKeys()
    {
        foreach (var entry in ChangeTracker.Entries<IHasNormalizedKeys>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.RefreshNormalizedKeys();
            }
        }
    }
}
