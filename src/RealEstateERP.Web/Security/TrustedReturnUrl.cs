using Microsoft.AspNetCore.Mvc;

namespace RealEstateERP.Web.Security;

/// <summary>
/// Where it's safe to send people back to after signing in or choosing a business: pages on this site, or our other
/// sites listed in Auth:TrustedReturnHosts (e.g. booking.shambanyumba.com). Anything else is ignored: no open redirects.
/// </summary>
public static class TrustedReturnUrl
{
    public static string? Check(string? returnUrl, IUrlHelper url, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return null;
        if (url.IsLocalUrl(returnUrl)) return returnUrl;
        var trusted = (config["Auth:TrustedReturnHosts"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
               && trusted.Contains(uri.Authority, StringComparer.OrdinalIgnoreCase) ? returnUrl : null;
    }
}
