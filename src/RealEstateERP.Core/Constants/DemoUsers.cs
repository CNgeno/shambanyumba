namespace RealEstateERP.Core.Constants;

/// <summary>The public demo's logins (one click, no password). The domain doesn't exist, so nothing can be emailed to them.</summary>
public static class DemoUsers
{
    public const string Owner = "demo@smartbiz.demo";
    public const string Agent = "demo.agent@smartbiz.demo";

    public static bool IsDemo(string? email) => email is not null && email.EndsWith("@smartbiz.demo", StringComparison.OrdinalIgnoreCase);
}
