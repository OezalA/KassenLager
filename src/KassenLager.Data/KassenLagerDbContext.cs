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

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Movement> Movements => Set<Movement>();

    public DbSet<BranchIssue> BranchIssues => Set<BranchIssue>();

    public DbSet<MinimumStock> MinimumStocks => Set<MinimumStock>();

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit registration instead of assembly scanning keeps the model trim-friendly.
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new ArticleConfiguration());
        modelBuilder.ApplyConfiguration(new AppSettingConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new MovementConfiguration());
        modelBuilder.ApplyConfiguration(new BranchIssueConfiguration());
        modelBuilder.ApplyConfiguration(new MinimumStockConfiguration());
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
