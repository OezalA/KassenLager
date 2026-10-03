using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KassenLager.Data;

/// <summary>Brings the database file to the latest schema on app start.</summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<KassenLagerDbContext> dbFactory,
    ILogger<DatabaseInitializer> logger)
{
    public void Initialize()
    {
        using var db = dbFactory.CreateDbContext();

        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
        }

        db.Database.Migrate();

        // WAL is persisted in the database file; backups must checkpoint first (Phase 3).
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}
