using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-03-B. Block / building / wing / floor inside an asset. Self-referencing tree of any depth.
/// Moving a node under its own descendant is blocked by the service (StructureTree.IsSelfOrDescendant).
/// </summary>
public class StructureNode : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public int? ParentId { get; set; }
    public StructureNode? Parent { get; set; }
    public ICollection<StructureNode> Children { get; set; } = new List<StructureNode>();

    public string Name { get; set; } = string.Empty;
    public StructureNodeType NodeType { get; set; }
    public int SortOrder { get; set; }
}
