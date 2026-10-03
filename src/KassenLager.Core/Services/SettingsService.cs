using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

/// <summary>Settings stored in the database (included in backups), as opposed to device preferences.</summary>
public sealed class SettingsService(IAppDbContextFactory dbFactory)
{
    public const int UserNameMaxLength = 100;

    internal const string UserNameKey = "UserName";

    /// <summary>Name of the person counting; printed on reports.</summary>
    public Task<string?> GetUserNameAsync(CancellationToken ct = default) => GetAsync(UserNameKey, ct);

    public Task SetUserNameAsync(string? userName, CancellationToken ct = default) =>
        SetAsync(UserNameKey, InputGuard.Optional(userName, "Benutzername", UserNameMaxLength), ct);

    private async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
    }

    private async Task SetAsync(string key, string? value, CancellationToken ct)
    {
        await using var db = dbFactory.CreateDbContext();
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync(ct);
    }
}
