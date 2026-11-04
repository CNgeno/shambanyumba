using System.Globalization;
using System.Text.RegularExpressions;

namespace RealEstateERP.Core.Common;

/// <summary>A property's map pin: validation and the links that open it in Google Maps (the app on phones).</summary>
public static class MapPin
{
    /// <summary>Null when the pin is fine (both set, in range) or absent (both null).</summary>
    public static string? Validate(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null) return null;
        if (latitude is null || longitude is null) return "Set both latitude and longitude for the map pin, or clear it.";
        if (double.IsNaN(latitude.Value) || latitude is < -90 or > 90) return "The map pin's latitude must be between -90 and 90.";
        if (double.IsNaN(longitude.Value) || longitude is < -180 or > 180) return "The map pin's longitude must be between -180 and 180.";
        return null;
    }

    /// <summary>6 decimals is about 10 cm: plenty, and keeps links short.</summary>
    public static double Round(double value) => Math.Round(value, 6);

    public static string Coordinates(double latitude, double longitude)
        => string.Create(CultureInfo.InvariantCulture, $"{latitude:0.######},{longitude:0.######}");

    public static string OpenUrl(double latitude, double longitude)
        => $"https://www.google.com/maps/search/?api=1&query={Coordinates(latitude, longitude)}";

    public static string DirectionsUrl(double latitude, double longitude)
        => $"https://www.google.com/maps/dir/?api=1&destination={Coordinates(latitude, longitude)}";

    /// <summary>Google Maps without an API key, for an iframe: the property with Google's red pin.</summary>
    public static string EmbedUrl(double latitude, double longitude, int zoom = 16)
        => $"https://maps.google.com/maps?q={Coordinates(latitude, longitude)}&z={zoom}&output=embed";

    private const string Num = @"(-?\d{1,3}(?:\.\d+)?)";
    // Most specific first: !3d!4d is the dropped pin / place itself, @lat,lng is only where the screen was centred.
    private static readonly Regex[] LinkPatterns =
    [
        new(@"!3d(-?\d{1,2}\.\d+)!4d(-?\d{1,3}\.\d+)", RegexOptions.Compiled),
        new(@"[?&](?:q|query|ll|destination|daddr|center)=(?:loc:)?" + Num + @"(?:,|%2C)(?:\+|%20|\s)*" + Num, RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"/(?:search|place|dir/[^/]*)/" + Num + @"(?:,|%2C)(?:\+|%20|\s)*" + Num, RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"@" + Num + "," + Num, RegexOptions.Compiled),
        new(@"^\s*" + Num + @"\s*[,\s]\s*" + Num + @"\s*$", RegexOptions.Compiled),
    ];
    // 1°17'11.0"S 36°49'01.8"E, as Google shows a dropped pin.
    private static readonly Regex Dms = new(
        @"(\d{1,3})°\s*(\d{1,2})['′]\s*(\d{1,2}(?:\.\d+)?)[""″]?\s*([NS])[,\s]+(\d{1,3})°\s*(\d{1,2})['′]\s*(\d{1,2}(?:\.\d+)?)[""″]?\s*([EW])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Coordinates from pasted text: "lat, lng", degrees-minutes-seconds, or a full Google Maps / OpenStreetMap link.</summary>
    public static (double Latitude, double Longitude)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = Uri.UnescapeDataString(text.Trim().Replace("+", " ")).Trim();

        var dms = Dms.Match(text);
        if (dms.Success)
        {
            double Part(int i) => double.Parse(dms.Groups[i].Value, CultureInfo.InvariantCulture);
            var lat = (Part(1) + Part(2) / 60 + Part(3) / 3600) * (dms.Groups[4].Value.Equals("S", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
            var lng = (Part(5) + Part(6) / 60 + Part(7) / 3600) * (dms.Groups[8].Value.Equals("W", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
            if (Validate(lat, lng) is null) return (Round(lat), Round(lng));
        }
        foreach (var pattern in LinkPatterns)
        {
            var m = pattern.Match(text);
            if (!m.Success) continue;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
                && Validate(lat, lng) is null)
                return (Round(lat), Round(lng));
        }
        return null;
    }

    /// <summary>Short share links (WhatsApp, the Google Maps app) that must be followed on the server to find the pin.</summary>
    public static bool IsShortLink(string? text)
        => Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
           && (u.Host.Equals("maps.app.goo.gl", StringComparison.OrdinalIgnoreCase)
               || (u.Host.Equals("goo.gl", StringComparison.OrdinalIgnoreCase) && u.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase)));
}
