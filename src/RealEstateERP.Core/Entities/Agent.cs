using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-10-A. An estate agent working for the agency.</summary>
public class Agent : TenantEntity
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Estate Agents Registration Board (EARB) practising licence.</summary>
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Agent's share of the agency's commission on contracts (and sales) assigned to them, 0-100.</summary>
    public decimal CommissionSharePercent { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The team member's login (AspNetUsers.Id) when the agent signs in. A linked member with the User or Viewer
    /// role is an "agent" and sees only their own contracts (see IAgentScopeService). One login per agent.
    /// </summary>
    public string? UserId { get; set; }
}
