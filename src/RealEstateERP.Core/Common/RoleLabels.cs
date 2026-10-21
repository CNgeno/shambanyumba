using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>
/// How a role is shown. Permissions never change; only the wording does. In an agency the account Owner is the
/// Principal (so "owner" doesn't clash with property owners), and a member linked to an agent profile is an Agent.
/// </summary>
public static class RoleLabels
{
    public static string For(BusinessRole role, BusinessType? businessType, bool isLinkedAgent = false)
    {
        if (isLinkedAgent && role is BusinessRole.User or BusinessRole.Viewer) return "Agent";
        if (businessType == BusinessType.Agency && role == BusinessRole.Owner) return "Principal";
        return role.ToString();
    }
}
