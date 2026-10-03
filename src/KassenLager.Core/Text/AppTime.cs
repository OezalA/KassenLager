namespace KassenLager.Core.Text;

/// <summary>
/// Times are stored in UTC and shown in German local time (Europe/Berlin),
/// independent of the device's time zone setting.
/// </summary>
public static class AppTime
{
    public static TimeZoneInfo Zone { get; } = FindZone();

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    public static DateOnly Today(TimeProvider clock) => ToLocalDate(clock.GetUtcNow().UtcDateTime);

    /// <summary>Converts a local (Berlin) wall-clock time to UTC; times in the DST gap move forward.</summary>
    public static DateTime LocalToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
    }

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
