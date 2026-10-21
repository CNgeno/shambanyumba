using System.Globalization;

namespace RealEstateERP.Web.Formatting;

/// <summary>NFR-04: stored in UTC, shown in EAT (UTC+3).</summary>
public static class DisplayFormat
{
    private static readonly TimeZoneInfo Eat = ResolveEat();

    private static TimeZoneInfo ResolveEat()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("EAT", TimeSpan.FromHours(3), "East Africa Time", "EAT");
        }
    }

    public static DateTime ToEat(this DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Eat);

    public static string ToEatDate(this DateTime utc)
        => utc.ToEat().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string ToEatDate(this DateTime? utc) => utc?.ToEatDate() ?? "";

    public static string ToEatDateTime(this DateTime utc)
        => utc.ToEat().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static string ToEatDateTime(this DateTime? utc) => utc?.ToEatDateTime() ?? "";

    public static string Kes(this decimal amount)
        => "KES " + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string UnitLimitText(this int? limit)
        => limit is int l ? $"Up to {l} units" : "Unlimited units";

    public static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
