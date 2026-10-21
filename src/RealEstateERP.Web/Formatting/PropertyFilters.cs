using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Formatting;

/// <summary>Letting pages offer only properties to let; selling pages only properties for sale.</summary>
public static class PropertyFilters
{
    public static IReadOnlyList<AssetSummary> ToLet(this IEnumerable<AssetSummary> assets) => assets.Where(a => PropertyRules.IsToLet(a.Purpose)).ToList();
    public static IReadOnlyList<AssetSummary> ForSale(this IEnumerable<AssetSummary> assets) => assets.Where(a => PropertyRules.IsForSale(a.Purpose)).ToList();
}
