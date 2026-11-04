using System.Globalization;

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
}
