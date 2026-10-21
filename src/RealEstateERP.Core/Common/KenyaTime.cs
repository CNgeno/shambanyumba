namespace RealEstateERP.Core.Common;

/// <summary>Kenya is UTC+3 all year (no daylight saving).</summary>
public static class KenyaTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateOnly TodayEat(this TimeProvider clock)
        => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(Offset).DateTime);
}
