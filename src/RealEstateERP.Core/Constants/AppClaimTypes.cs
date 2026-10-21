namespace RealEstateERP.Core.Constants;

public static class AppClaimTypes
{
    public const string BusinessId = "smartbiz:business_id";
    public const string BusinessRole = "smartbiz:business_role";
    public const string BusinessName = "smartbiz:business_name";
    public const string BusinessType = "smartbiz:business_type";
    public const string RentalTools = "smartbiz:rental_tools";
    public const string FullName = "smartbiz:full_name";

    public static readonly IReadOnlyList<string> BusinessContext = [BusinessId, BusinessRole, BusinessName, BusinessType, RentalTools];
}

public static class AppRoles
{
    /// <summary>Platform operator (approves subscription payments etc.). Not a per-business role.</summary>
    public const string SuperAdmin = "SuperAdmin";
}
