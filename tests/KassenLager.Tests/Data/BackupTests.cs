using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Data;
using KassenLager.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KassenLager.Tests.Data;

/// <summary>Backup and restore against a real database file in a temporary folder.</summary>
public sealed class BackupTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("kassenlager-tests-").FullName;
    private readonly string _databasePath;
    private readonly FileContextFactory _factory;
    private readonly DatabaseInitializer _initializer;
    private readonly DatabaseBackupService _backups;

    public BackupTests()
    {
        _databasePath = Path.Combine(_directory, "kassenlager.db");
        _factory = new FileContextFactory(_databasePath);
        _initializer = new DatabaseInitializer(_factory, NullLogger<DatabaseInitializer>.Instance);
        _initializer.Initialize();
        _backups = new DatabaseBackupService(new DatabaseLocation(_databasePath), _initializer, new TestClock(), NullLogger<DatabaseBackupService>.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task BackupAndRestore_BringsBackTheSavedState_AndKeepsAnAutomaticBackup()
    {
        await AddArticleAsync("Vor der Sicherung");
        var backup = await _backups.CreateBackupAsync(Path.Combine(_directory, "export"));
        await AddArticleAsync("Nach der Sicherung");

        var info = await _backups.InspectAsync(backup);
        var automatic = await _backups.RestoreAsync(backup, Path.Combine(_directory, "auto"));

        Assert.Equal(1, info.Articles);
        Assert.Equal(["Vor der Sicherung"], await ArticleNamesAsync());
        Assert.Equal(2, (await _backups.InspectAsync(automatic)).Articles);
    }

    [Fact]
    public async Task Inspect_FileThatIsNoDatabase_Throws()
    {
        var path = Path.Combine(_directory, "kaputt.db");
        await File.WriteAllTextAsync(path, "keine Datenbank");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _backups.InspectAsync(path));
        Assert.Equal(Messages.BackupInvalidFile, ex.Message);
    }

    [Fact]
    public async Task Inspect_BackupFromNewerAppVersion_Throws()
    {
        var backup = await _backups.CreateBackupAsync(Path.Combine(_directory, "export"));
        await using (var connection = new SqliteConnection($"Data Source={backup};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory VALUES ('29991231000000_FromTheFuture', '99.0.0');";
            await command.ExecuteNonQueryAsync();
        }

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _backups.InspectAsync(backup));
        Assert.Equal(Messages.BackupFromNewerVersion, ex.Message);
    }

    [Fact]
    public async Task Restore_InvalidFile_LeavesTheDataUntouched()
    {
        await AddArticleAsync("Bleibt");
        var path = Path.Combine(_directory, "kaputt.db");
        await File.WriteAllTextAsync(path, "keine Datenbank");

        await Assert.ThrowsAsync<BusinessRuleException>(() => _backups.RestoreAsync(path, Path.Combine(_directory, "auto")));

        Assert.Equal(["Bleibt"], await ArticleNamesAsync());
    }

    private async Task AddArticleAsync(string name)
    {
        await using var db = _factory.CreateDbContext();
        db.Articles.Add(new Article { Name = name, CategoryId = TestDatabase.QuantityCategoryId, UnitId = TestDatabase.PieceUnitId });
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> ArticleNamesAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Articles.Select(a => a.Name).ToListAsync();
    }

    private sealed class FileContextFactory(string path) : IDbContextFactory<KassenLagerDbContext>
    {
        public KassenLagerDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<KassenLagerDbContext>().UseSqlite($"Data Source={path}").Options);
    }
}
