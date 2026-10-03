using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KassenLager.Data.Configurations;

/// <summary>
/// All <see cref="DateTime"/> columns hold UTC. SQLite stores them as sortable text without
/// a kind, so values read back are marked as UTC again.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
