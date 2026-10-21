using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

public static class PropertyRules
{
    /// <summary>The purpose a property starts with: land is for sale, everything else is to let.</summary>
    public static PropertyPurpose DefaultPurpose(AssetType type) => type == AssetType.Land ? PropertyPurpose.Sell : PropertyPurpose.Let;

    public static PropertyPurpose Effective(PropertyPurpose? purpose, AssetType type) => purpose ?? DefaultPurpose(type);

    public static bool IsForSale(PropertyPurpose purpose) => purpose is PropertyPurpose.Sell or PropertyPurpose.Both;
    public static bool IsToLet(PropertyPurpose purpose) => purpose is PropertyPurpose.Let or PropertyPurpose.Both;
}

public static class PlanRules
{
    /// <summary>Agency features (owners, contracts, owner statements, agent logins): during the trial, and on plans that include them.</summary>
    public static bool AllowsAgency(string planCode, bool? includesAgency, bool isTrial)
        => isTrial || (includesAgency ?? planCode is "AGENT" or "ENTERPRISE");
}
