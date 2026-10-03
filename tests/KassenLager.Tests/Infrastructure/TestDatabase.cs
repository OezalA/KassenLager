using KassenLager.Core.Abstractions;
using KassenLager.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Tests.Infrastructure;

/// <summary>
/// A migrated SQLite in-memory database per test. The connection stays open for the
/// lifetime of the instance, so every context created from it sees the same data.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    // Seeded ids, see SeedData.
    public const int SerialCategoryId = 1;    // Kassenrechner
    public const int QuantityCategoryId = 13; // Kabel
    public const int PieceUnitId = 1;         // Stück

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<KassenLagerDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<KassenLagerDbContext>().UseSqlite(_connection).Options;

        using var db = CreateContext();
        db.Database.Migrate();

        Factory = new ContextFactory(this);
    }

    public IAppDbContextFactory Factory { get; }

    public KassenLagerDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();

    private sealed class ContextFactory(TestDatabase database) : IAppDbContextFactory
    {
        public IAppDbContext CreateDbContext() => database.CreateContext();
    }
}
