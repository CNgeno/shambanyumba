using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure marketplace rules. No database; fully unit-tested.</summary>
public static class MarketplaceRules
{
    public const int MaxPhotos = 12;
    public const int RepeatWindowDays = 30;          // same property, same person: free again within 30 days
    public const int OwnerRepeatFreeDays = 10;       // same owner (any of their properties), same person: free within 10 days
    public const int ConfirmedEmailValidDays = 10;   // a confirmed email skips confirmation in the same browser for 10 days
    public static readonly TimeSpan VerifyWindow = TimeSpan.FromHours(48);

    public static string? ValidateListing(string title, string description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 10) return "Give the listing a clear title (at least 10 characters).";
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 40) return "Describe the property in a few sentences (at least 40 characters).";
        if (price <= 0) return "Enter the price.";
        return null;
    }

    /// <summary>A unit can be advertised only while it's free: Available (for rent or sale).</summary>
    public static bool UnitIsAdvertisable(UnitStatus status) => status == UnitStatus.Available;

    public static string? RefundError(LeadStatus status, DateTime? unlockedAtUtc, bool isRepeat, DateTime nowUtc, int refundDays, int refundsThisMonth, int maxRefundsPerMonth)
    {
        if (status != LeadStatus.Unlocked || isRepeat) return "Only paid leads can be refunded.";
        if (unlockedAtUtc is not DateTime unlocked || nowUtc - unlocked > TimeSpan.FromDays(refundDays))
            return $"Leads can be reported within {refundDays} days.";
        if (refundsThisMonth >= maxRefundsPerMonth) return $"You've reached this month's limit of {maxRefundsPerMonth} refunds. Contact support if something's wrong.";
        return null;
    }

    /// <summary>"2-bedroom apartment in Ruaka" style default title.</summary>
    public static string SuggestTitle(ListingType type, UnitType unitType, int? bedrooms, decimal? sizeSqm, string? location)
    {
        var what = unitType switch
        {
            UnitType.Plot => sizeSqm is decimal s ? $"{s:0}sqm plot" : "Plot",
            _ when bedrooms is int b && b > 0 => $"{b}-bedroom {Lower(unitType)}",
            _ when bedrooms == 0 => $"Bedsitter {Lower(unitType)}",
            _ => Capital(Lower(unitType))
        };
        var where = string.IsNullOrWhiteSpace(location) ? "" : $" in {location.Split(',')[0].Trim()}";
        return $"{Capital(what)}{where}{(type == ListingType.Sale ? " for sale" : " to let")}";
    }

    public static string Slug(string title)
    {
        var chars = title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }

    private static string Lower(UnitType t) => t switch
    {
        UnitType.Bedsitter => "bedsitter",
        _ => System.Text.RegularExpressions.Regex.Replace(t.ToString(), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant()
    };

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
