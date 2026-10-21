using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>FR-10-A/B: owners, agents and management contracts. Depends on: DbContext, INumberSequenceService, TimeProvider.</summary>
public interface IAgencyService
{
    Task<IReadOnlyList<OwnerSummary>> ListOwnersAsync(string? search, CancellationToken ct = default);
    Task<PropertyOwner?> GetOwnerAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveOwnerAsync(int? id, OwnerInput input, CancellationToken ct = default);

    Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(bool includeInactive, CancellationToken ct = default);
    Task<Agent?> GetAgentAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAgentAsync(int? id, AgentInput input, CancellationToken ct = default);
    Task<IReadOnlyList<LinkableMember>> GetLinkableMembersAsync(CancellationToken ct = default);
    /// <summary>Moves every active and draft contract from one agent to another (or to no agent). Principal/Admin only.</summary>
    Task<Result<int>> ReassignContractsAsync(int fromAgentId, int? toAgentId, CancellationToken ct = default);

    Task<IReadOnlyList<ContractSummary>> ListContractsAsync(int? ownerId, Enums.ContractStatus? status, CancellationToken ct = default);
    Task<ContractDetails?> GetContractAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AvailableProperty>> GetAvailablePropertiesAsync(CancellationToken ct = default);
    Task<Result<int>> CreateContractAsync(ContractInput input, CancellationToken ct = default);
    Task<Result> UpdateContractAsync(int id, ContractInput input, CancellationToken ct = default);
    Task<Result> AddPropertyAsync(int contractId, int assetId, CancellationToken ct = default);
    Task<Result> RemovePropertyAsync(int contractId, int linkId, CancellationToken ct = default);
    Task<Result> ActivateAsync(int contractId, CancellationToken ct = default);
    Task<Result> EndAsync(int contractId, DateOnly endedOn, CancellationToken ct = default);

    /// <summary>Owner currently managing each asset (for property pages).</summary>
    Task<IReadOnlyDictionary<int, (int OwnerId, string OwnerName, int ContractId)>> GetManagedAssetsAsync(CancellationToken ct = default);
}

/// <summary>
/// LOW-LEVEL. Works out whether the signed-in member is a linked agent (restricted to their own contracts).
/// Looked up from the database and cached for 2 minutes per user, so linking or unlinking applies quickly.
/// Depends on: DbContext, ICurrentUserService, IMemoryCache.
/// </summary>
public interface IAgentScopeService
{
    Task<AgentScope> GetAsync(CancellationToken ct = default);
    void Invalidate(int businessId, string userId);
}

/// <summary>FR-10-C: owner remittances and agent commission. Depends on: DbContext, INumberSequenceService, TimeProvider.</summary>
public interface IRemittanceService
{
    Task<RemittancePreview?> PreviewAsync(int contractId, int year, int month, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(int contractId, int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<RemittanceSummary>> ListAsync(DateOnly? month, Enums.RemittanceStatus? status, int? ownerId, CancellationToken ct = default);
    Task<RemittanceDetails?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> AddAdjustmentAsync(int id, string description, decimal amount, CancellationToken ct = default);
    Task<Result> RemoveAdjustmentAsync(int id, int lineId, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result> MarkPaidAsync(int id, DateOnly paidOn, Enums.PayoutMethod method, string? reference, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<AgentCommissionRow>> AgentCommissionsAsync(int year, int month, CancellationToken ct = default);
}
