namespace KassenLager.Tests.Infrastructure;

/// <summary>Controllable clock; starts at 03.10.2026 12:00 Berlin time (CEST).</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime.AddHours(2));

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}
