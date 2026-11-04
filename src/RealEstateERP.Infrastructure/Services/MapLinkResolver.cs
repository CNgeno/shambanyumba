using System.Text.RegularExpressions;
using RealEstateERP.Core.Common;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Turns a pasted Google Maps link into a pin. Short share links (maps.app.goo.gl, from WhatsApp or the Maps app) are
/// followed hop by hop, only ever to Google hosts, until the full link shows the coordinates.
/// </summary>
public sealed class MapLinkResolver(HttpClient http)
{
    private const int MaxHops = 6;
    private const int MaxPageChars = 600_000;

    private static readonly Regex PageCentre = new(@"center=(-?\d{1,2}\.\d+)%2C(-?\d{1,3}\.\d+)", RegexOptions.Compiled);

    private static bool IsGoogleHost(Uri u) => u.Scheme == Uri.UriSchemeHttps && (
        u.Host is "maps.app.goo.gl" or "goo.gl" or "google.com" or "maps.google.com" or "consent.google.com"
        || u.Host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)
        || Regex.IsMatch(u.Host, @"^(www\.|maps\.)?google\.co(m)?(\.[a-z]{2})?$", RegexOptions.IgnoreCase));

    public async Task<(double Latitude, double Longitude)?> ResolveAsync(string? text, CancellationToken ct = default)
    {
        if (MapPin.Parse(text) is { } direct) return direct;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var url) || !IsGoogleHost(url)) return null;

        for (var hop = 0; hop < MaxHops; hop++)
        {
            if (MapPin.Parse(url.ToString()) is { } found) return found;

            // A consent wall (EU visitors) carries the real link in "continue".
            if (url.Host == "consent.google.com")
            {
                var cont = System.Web.HttpUtility.ParseQueryString(url.Query)["continue"];
                if (cont is null || !Uri.TryCreate(cont, UriKind.Absolute, out var next) || !IsGoogleHost(next)) return null;
                url = next;
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ShambaNyumba map-pin)");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                var next = location.IsAbsoluteUri ? location : new Uri(url, location);
                if (!IsGoogleHost(next)) return null;
                url = next;
                continue;
            }
            if (!response.IsSuccessStatusCode) return null;

            // Final page: Google puts the map centre (the pin) in its static-map preview URL.
            var page = await response.Content.ReadAsStringAsync(ct);
            if (page.Length > MaxPageChars) page = page[..MaxPageChars];
            var m = PageCentre.Match(page);
            if (m.Success && MapPin.Parse($"{m.Groups[1].Value},{m.Groups[2].Value}") is { } centre) return centre;
            return MapPin.Parse(page.Contains("!3d") ? Regex.Match(page, @"!3d-?\d+\.\d+!4d-?\d+\.\d+").Value : null);
        }
        return null;
    }
}
