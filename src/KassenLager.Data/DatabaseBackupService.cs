using KassenLager.Core;
using KassenLager.Core.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KassenLager.Data;

/// <summary>Full path of the app's SQLite database file.</summary>
public sealed record DatabaseLocation(string Path);

/// <summary>What a backup file contains, shown before restoring it.</summary>
public sealed record BackupInfo(int Articles, int Devices, int Movements, DateTime? LastMovementAt)
{
    public DateTime? LastMovementLocalTime => LastMovementAt is { } at ? AppTime.ToLocal(at) : null;
}

/// <summary>
/// Full backup as a single database file and restore from such a file. Backups are consistent
/// copies (WAL checkpoint, then VACUUM INTO); before a restore the current data is backed up
/// automatically, and older backups are migrated to the current schema.
/// </summary>
public sealed class DatabaseBackupService(
    DatabaseLocation location,
    DatabaseInitializer initializer,
    TimeProvider clock,
    ILogger<DatabaseBackupService> logger)
{
    private const int KeptAutomaticBackups = 5;

    /// <returns>Path of the new backup file in <paramref name="directory"/>.</returns>
    public async Task<string> CreateBackupAsync(string directory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var local = AppTime.ToLocal(clock.GetUtcNow().UtcDateTime);
        var path = Path.Combine(directory, $"KassenLager-Sicherung_{local:yyyy-MM-dd_HHmmss}.db");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = location.Path, Pooling = false }.ToString());
        await connection.OpenAsync(ct);
        await ExecuteAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE);", ct);

        await using var vacuum = connection.CreateCommand();
        vacuum.CommandText = "VACUUM INTO $path;";
        vacuum.Parameters.AddWithValue("$path", path);
        await vacuum.ExecuteNonQueryAsync(ct);
        return path;
    }

    /// <summary>Checks that the file is a KassenLager database this app version can read.</summary>
    public async Task<BackupInfo> InspectAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            await connection.OpenAsync(ct);

            var applied = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    applied.Add(reader.GetString(0));
                }
            }

            var known = initializer.GetKnownMigrations();
            if (applied.Count == 0 || !applied.Contains(known[0]))
            {
                throw new BusinessRuleException(Messages.BackupInvalidFile);
            }

            if (applied.Except(known).Any())
            {
                throw new BusinessRuleException(Messages.BackupFromNewerVersion);
            }

            return new BackupInfo(
                await CountAsync(connection, "Articles", ct),
                await CountAsync(connection, "Devices", ct),
                await CountAsync(connection, "Movements", ct),
                await LastMovementAsync(connection, ct));
        }
        catch (SqliteException ex)
        {
            logger.LogWarning(ex, "Backup file {Path} could not be read", path);
            throw new BusinessRuleException(Messages.BackupInvalidFile);
        }
    }

    /// <summary>
    /// Replaces the database with the backup. The current data is saved to
    /// <paramref name="automaticBackupDirectory"/> first and put back if the backup cannot be opened.
    /// </summary>
    /// <returns>Path of the automatic backup of the replaced data.</returns>
    public async Task<string> RestoreAsync(string path, string automaticBackupDirectory, CancellationToken ct = default)
    {
        await InspectAsync(path, ct);
        var automaticBackup = await CreateBackupAsync(automaticBackupDirectory, ct);

        try
        {
            ReplaceDatabaseFile(path);
            initializer.Initialize();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore failed; putting the previous data back");
            ReplaceDatabaseFile(automaticBackup);
            initializer.Initialize();
            throw new BusinessRuleException(Messages.BackupInvalidFile);
        }

        DeleteOldBackups(automaticBackupDirectory);
        return automaticBackup;
    }

    private void ReplaceDatabaseFile(string source)
    {
        // Pooled connections would keep the old file open.
        SqliteConnection.ClearAllPools();

        var temp = location.Path + ".restore";
        File.Copy(source, temp, overwrite: true);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(location.Path + suffix))
            {
                File.Delete(location.Path + suffix);
            }
        }

        File.Move(temp, location.Path, overwrite: true);
    }

    private static void DeleteOldBackups(string directory)
    {
        foreach (var file in new DirectoryInfo(directory).GetFiles("KassenLager-Sicherung_*.db")
                     .OrderByDescending(f => f.Name)
                     .Skip(KeptAutomaticBackups))
        {
            file.Delete();
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
            return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        }
        catch (SqliteException)
        {
            return 0; // Backups from before the table existed.
        }
    }

    private static async Task<DateTime?> LastMovementAsync(SqliteConnection connection, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(OccurredAt) FROM Movements;";
            return await command.ExecuteScalarAsync(ct) is string text
                ? DateTime.SpecifyKind(DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc)
                : null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}
