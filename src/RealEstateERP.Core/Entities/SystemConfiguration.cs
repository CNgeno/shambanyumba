using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-02-D. Platform-wide settings, admin-editable.</summary>
public class SystemConfiguration : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ConfigValueType ValueType { get; set; } = ConfigValueType.Text;
    public string Group { get; set; } = "General";
    public string? Description { get; set; }
}
