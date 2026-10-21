using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Who sees the rental menus: the business's own choice, else everyone except developers.</summary>
public static class RentalTools
{
    public static bool Enabled(BusinessType? type, bool? choice) => choice ?? type != BusinessType.Developer;
}
