using System.Security.Claims;
using RealEstateERP.Core.Constants;

namespace RealEstateERP.Web.Formatting;

public static class UserDisplay
{
    public static string DisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue(AppClaimTypes.FullName) is { Length: > 0 } name ? name : user.Identity?.Name ?? "";

    public static string FirstName(this ClaimsPrincipal user)
    {
        var name = user.FindFirstValue(AppClaimTypes.FullName);
        return string.IsNullOrWhiteSpace(name) ? "" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    /// <summary>"Wanjiku Kamau" -> "WK"; "owner@x.test" -> "O".</summary>
    public static string Initials(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "?";
        var source = text.Contains('@') ? text[..text.IndexOf('@')] : text;
        var parts = source.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : source[..1];
        return initials.ToUpperInvariant();
    }

    public static string Greeting(DateTime utcNow) => utcNow.ToEat().Hour switch
    {
        < 12 => "Good morning",
        < 17 => "Good afternoon",
        _ => "Good evening"
    };
}
