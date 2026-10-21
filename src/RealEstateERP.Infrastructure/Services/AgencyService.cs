using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-10-A/B. Depends on: DbContext, INumberSequenceService, IAgentScopeService (low-level), ICurrentUserService, TimeProvider.
/// Restricted agents see only their own contracts and the owners on them, and can't change contracts or owners.
/// </summary>
public sealed class AgencyService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    IAgentScopeService agentScope,
    ICurrentUserService currentUser,
    TimeProvider clock) : IAgencyService
{
    private const string PrincipalOnly = "Only the principal or an admin can change this.";
    private DateOnly Today => clock.TodayEat();
    private Task<AgentScope> ScopeAsync(CancellationToken ct) => agentScope.GetAsync(ct);

    // ------------------------------------------------------------------ owners

    public async Task<IReadOnlyList<OwnerSummary>> ListOwnersAsync(string? search, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        var query = db.PropertyOwners.AsNoTracking();
        if (scope.IsRestricted) query = query.Where(o => db.ManagementContracts.Any(c => c.OwnerId == o.Id && c.AgentId == scope.AgentId));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(o => o.FullName.Contains(search.Trim()) || (o.Phone != null && o.Phone.Contains(search.Trim())));
        var owners = await query.OrderBy(o => o.FullName).Select(o => new { o.Id, o.FullName, o.Type, o.Phone, o.Email }).ToListAsync(ct);

        var stats = await db.ManagementContracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active && (!scope.IsRestricted || c.AgentId == scope.AgentId))
            .Select(c => new { c.OwnerId, Props = c.Properties.Count(p => p.IsActive) })
            .ToListAsync(ct);
        return owners.Select(o => new OwnerSummary(o.Id, o.FullName, o.Type, o.Phone, o.Email,
            stats.Count(s => s.OwnerId == o.Id), stats.Where(s => s.OwnerId == o.Id).Sum(s => s.Props))).ToList();
    }

    public async Task<PropertyOwner?> GetOwnerAsync(int id, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        return await db.PropertyOwners.AsNoTracking()
            .Where(o => !scope.IsRestricted || db.ManagementContracts.Any(c => c.OwnerId == o.Id && c.AgentId == scope.AgentId))
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Result<int>> SaveOwnerAsync(int? id, OwnerInput input, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result<int>.Fail(PrincipalOnly);
        if (string.IsNullOrWhiteSpace(input.FullName)) return Result<int>.Fail("Enter the owner's name.");
        string? phone = null, mpesa = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && (phone = KenyanPhone.Normalize(input.Phone)) is null) return Result<int>.Fail("Enter a valid phone number.");
        if (!string.IsNullOrWhiteSpace(input.MpesaNumber) && (mpesa = KenyanPhone.Normalize(input.MpesaNumber)) is null) return Result<int>.Fail("Enter a valid M-PESA number.");
        if (input.PayoutMethod == PayoutMethod.Bank && (string.IsNullOrWhiteSpace(input.BankName) || string.IsNullOrWhiteSpace(input.BankAccountNumber)))
            return Result<int>.Fail("Enter the bank name and account number the owner is paid into.");
        if (input.PayoutMethod == PayoutMethod.Mpesa && mpesa is null) return Result<int>.Fail("Enter the M-PESA number the owner is paid on.");

        PropertyOwner owner;
        if (id is int existing)
        {
            var found = await db.PropertyOwners.FirstOrDefaultAsync(o => o.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Owner not found.");
            owner = found;
        }
        else
        {
            owner = new PropertyOwner();
            db.PropertyOwners.Add(owner);
        }

        owner.Type = input.Type;
        owner.FullName = input.FullName.Trim();
        owner.IdNumber = input.IdNumber?.Trim().ToUpperInvariant();
        owner.KraPin = input.KraPin?.Trim().ToUpperInvariant();
        owner.Phone = phone;
        owner.Email = input.Email?.Trim().ToLowerInvariant();
        owner.PostalAddress = input.PostalAddress?.Trim();
        owner.PayoutMethod = input.PayoutMethod;
        owner.BankName = input.BankName?.Trim();
        owner.BankAccountName = input.BankAccountName?.Trim();
        owner.BankAccountNumber = input.BankAccountNumber?.Trim();
        owner.MpesaNumber = mpesa;
        owner.Notes = input.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(owner.Id);
    }

    // ------------------------------------------------------------------ agents

    public async Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        var agents = await db.Agents.AsNoTracking()
            .Where(a => (includeInactive || a.IsActive) && (!scope.IsRestricted || a.Id == scope.AgentId))
            .OrderBy(a => a.FullName).ToListAsync(ct);
        var loginIds = agents.Where(a => a.UserId != null).Select(a => a.UserId!).ToList();
        var logins = await db.Users.AsNoTracking().Where(u => loginIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        var contracts = await db.ManagementContracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active && c.AgentId != null)
            .GroupBy(c => c.AgentId!.Value).Select(g => new { AgentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, ct);
        return agents.Select(a => new AgentSummary(a.Id, a.FullName, a.LicenseNumber, a.Phone, a.CommissionSharePercent, a.IsActive,
            contracts.GetValueOrDefault(a.Id), a.UserId is not null ? logins.GetValueOrDefault(a.UserId) : null)).ToList();
    }

    public Task<Agent?> GetAgentAsync(int id, CancellationToken ct = default)
        => db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Result<int>> SaveAgentAsync(int? id, AgentInput input, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result<int>.Fail(PrincipalOnly);
        if (string.IsNullOrWhiteSpace(input.FullName)) return Result<int>.Fail("Enter the agent's name.");
        var businessId = currentUser.BusinessId!.Value;
        if (input.UserId is string uid)
        {
            if (!await db.BusinessUsers.AnyAsync(m => m.BusinessId == businessId && m.UserId == uid && m.IsActive, ct))
                return Result<int>.Fail("That login isn't a member of this business. Invite them from Team first.");
            if (await db.Agents.AnyAsync(a => a.UserId == uid && a.Id != (id ?? 0), ct))
                return Result<int>.Fail("That login is already linked to another agent.");
        }
        if (input.CommissionSharePercent is < 0 or > 100) return Result<int>.Fail("The agent's share must be between 0 and 100%.");
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && (phone = KenyanPhone.Normalize(input.Phone)) is null) return Result<int>.Fail("Enter a valid phone number.");

        Agent agent;
        if (id is int existing)
        {
            var found = await db.Agents.FirstOrDefaultAsync(a => a.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Agent not found.");
            agent = found;
        }
        else
        {
            agent = new Agent();
            db.Agents.Add(agent);
        }
        agent.FullName = input.FullName.Trim();
        agent.LicenseNumber = input.LicenseNumber?.Trim().ToUpperInvariant();
        agent.Phone = phone;
        agent.Email = input.Email?.Trim().ToLowerInvariant();
        agent.CommissionSharePercent = input.CommissionSharePercent;
        agent.IsActive = input.IsActive;
        var previousLogin = agent.UserId;
        agent.UserId = input.UserId;
        await db.SaveChangesAsync(ct);

        // Linking or unlinking changes what someone can see: drop their cached scope now.
        if (previousLogin is not null) agentScope.Invalidate(businessId, previousLogin);
        if (input.UserId is not null) agentScope.Invalidate(businessId, input.UserId);
        return Result<int>.Ok(agent.Id);
    }

    public async Task<IReadOnlyList<LinkableMember>> GetLinkableMembersAsync(CancellationToken ct = default)
    {
        var businessId = currentUser.BusinessId!.Value;
        var members = await (from m in db.BusinessUsers.AsNoTracking()
                             join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                             where m.BusinessId == businessId && m.IsActive
                             orderby u.FullName
                             select new { u.Id, u.FullName, u.Email }).ToListAsync(ct);
        var linked = await db.Agents.AsNoTracking().Where(a => a.UserId != null).ToDictionaryAsync(a => a.UserId!, a => a.FullName, ct);
        return members.Select(m => new LinkableMember(m.Id, m.FullName ?? m.Email ?? m.Id, m.Email ?? "", linked.GetValueOrDefault(m.Id))).ToList();
    }

    public async Task<Result<int>> ReassignContractsAsync(int fromAgentId, int? toAgentId, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result<int>.Fail(PrincipalOnly);
        if (toAgentId == fromAgentId) return Result<int>.Fail("Choose a different agent.");
        if (toAgentId is int to && !await db.Agents.AnyAsync(a => a.Id == to && a.IsActive, ct)) return Result<int>.Fail("Choose an active agent to take them over.");

        var contracts = await db.ManagementContracts.Where(c => c.AgentId == fromAgentId && c.Status != ContractStatus.Ended).ToListAsync(ct);
        foreach (var c in contracts) c.AgentId = toAgentId;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(contracts.Count);
    }

    // ------------------------------------------------------------------ contracts

    public async Task<IReadOnlyList<ContractSummary>> ListContractsAsync(int? ownerId, ContractStatus? status, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        var query = db.ManagementContracts.AsNoTracking();
        if (scope.IsRestricted) query = query.Where(c => c.AgentId == scope.AgentId);
        if (ownerId is int o) query = query.Where(c => c.OwnerId == o);
        if (status is ContractStatus s) query = query.Where(c => c.Status == s);
        var rows = await query.OrderBy(c => c.Owner.FullName).ThenByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id, c.ContractNumber, c.OwnerId, OwnerName = c.Owner.FullName, c.AgentId, AgentName = c.Agent != null ? c.Agent.FullName : null,
                c.StartDate, c.EndDate, c.CommissionType, c.CommissionPercent, c.FixedMonthlyFee, c.Status,
                Props = c.Properties.Where(p => p.IsActive || c.Status != ContractStatus.Active).Select(p => p.Asset.Name).ToList()
            })
            .ToListAsync(ct);
        return rows.Select(r => new ContractSummary(r.Id, r.ContractNumber, r.OwnerId, r.OwnerName, r.AgentId, r.AgentName, r.StartDate, r.EndDate,
            r.CommissionType, r.CommissionPercent, r.FixedMonthlyFee, r.Status, r.Props.Count, r.Props.Count == 0 ? null : string.Join(", ", r.Props.Distinct()))).ToList();
    }

    public async Task<ContractDetails?> GetContractAsync(int id, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        var contract = await db.ManagementContracts.AsNoTracking().Include(c => c.Owner).Include(c => c.Agent)
            .FirstOrDefaultAsync(c => c.Id == id && (!scope.IsRestricted || c.AgentId == scope.AgentId), ct);
        if (contract is null) return null;

        var props = await db.ContractProperties.AsNoTracking().Where(p => p.ContractId == id)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Asset.Name)
            .Select(p => new ContractPropertyRow(p.Id, p.AssetId, p.Asset.Name, p.Asset.Units.Count, p.AddedOn, p.RemovedOn, p.IsActive))
            .ToListAsync(ct);
        var remittances = await db.OwnerRemittances.AsNoTracking().Where(r => r.ContractId == id)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new RemittanceSummary(r.Id, r.RemittanceNumber, r.ContractId, contract.ContractNumber, r.OwnerId, contract.Owner.FullName,
                r.PeriodStart, r.GrossCollections, r.CommissionAmount, r.NetPayable, r.Status, null))
            .ToListAsync(ct);
        return new ContractDetails(contract, props, remittances);
    }

    public async Task<IReadOnlyList<AvailableProperty>> GetAvailablePropertiesAsync(CancellationToken ct = default)
        => await db.Assets.AsNoTracking()
            .Where(a => !db.ContractProperties.Any(p => p.AssetId == a.Id && p.IsActive))
            .OrderBy(a => a.Name)
            .Select(a => new AvailableProperty(a.Id, a.Name, a.Units.Count))
            .ToListAsync(ct);

    public async Task<Result<int>> CreateContractAsync(ContractInput input, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result<int>.Fail(PrincipalOnly);
        var error = await ValidateAsync(input, ct);
        if (error is not null) return Result<int>.Fail(error);

        var number = await numbers.NextAsync(SequenceKeys.Contract, ct);
        var contract = new ManagementContract { ContractNumber = $"MC-{number:00000}", Status = ContractStatus.Draft };
        Apply(contract, input);
        db.ManagementContracts.Add(contract);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(contract.Id);
    }

    public async Task<Result> UpdateContractAsync(int id, ContractInput input, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var contract = await db.ManagementContracts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contract is null) return Result.Fail("Contract not found.");
        if (contract.Status == ContractStatus.Ended) return Result.Fail("An ended contract can't be changed.");
        if (contract.Status == ContractStatus.Active && input.OwnerId != contract.OwnerId) return Result.Fail("The owner of an active contract can't change. End it and start a new one.");

        var error = await ValidateAsync(input, ct);
        if (error is not null) return Result.Fail(error);
        Apply(contract, input);   // commission changes apply to remittances created from now on
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddPropertyAsync(int contractId, int assetId, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var contract = await db.ManagementContracts.FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null) return Result.Fail("Contract not found.");
        if (contract.Status == ContractStatus.Ended) return Result.Fail("This contract has ended.");
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct)) return Result.Fail("Property not found.");
        if (await db.ContractProperties.AnyAsync(p => p.AssetId == assetId && (p.IsActive || (p.ContractId == contractId && p.RemovedOn == null)), ct))
            return Result.Fail("That property is already under a management contract.");

        db.ContractProperties.Add(new ContractProperty
        {
            ContractId = contractId, AssetId = assetId,
            AddedOn = contract.Status == ContractStatus.Active ? Today : contract.StartDate,
            IsActive = contract.Status == ContractStatus.Active
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Fail("That property is already under another active contract."); }
        return Result.Ok();
    }

    public async Task<Result> RemovePropertyAsync(int contractId, int linkId, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var link = await db.ContractProperties.FirstOrDefaultAsync(p => p.Id == linkId && p.ContractId == contractId, ct);
        if (link is null) return Result.Fail("Property not found on this contract.");
        if (link.RemovedOn is not null) return Result.Fail("Already removed.");

        var contract = await db.ManagementContracts.FirstAsync(c => c.Id == contractId, ct);
        if (contract.Status == ContractStatus.Draft) db.ContractProperties.Remove(link);   // never active: just drop it
        else { link.IsActive = false; link.RemovedOn = Today; }   // keep history for past remittances
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ActivateAsync(int contractId, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var contract = await db.ManagementContracts.Include(c => c.Properties).FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null) return Result.Fail("Contract not found.");
        if (contract.Status != ContractStatus.Draft) return Result.Fail("Only draft contracts can be activated.");
        var links = contract.Properties.Where(p => p.RemovedOn == null).ToList();
        if (links.Count == 0) return Result.Fail("Add at least one property before activating.");

        var assetIds = links.Select(l => l.AssetId).ToList();
        if (await db.ContractProperties.AnyAsync(p => assetIds.Contains(p.AssetId) && p.IsActive && p.ContractId != contractId, ct))
            return Result.Fail("One of these properties is already under another active contract.");

        contract.Status = ContractStatus.Active;
        foreach (var l in links) l.IsActive = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Fail("One of these properties is already under another active contract."); }
        return Result.Ok();
    }

    public async Task<Result> EndAsync(int contractId, DateOnly endedOn, CancellationToken ct = default)
    {
        if ((await ScopeAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var contract = await db.ManagementContracts.Include(c => c.Properties).FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null) return Result.Fail("Contract not found.");
        if (contract.Status != ContractStatus.Active) return Result.Fail("Only active contracts can be ended.");
        if (endedOn < contract.StartDate) return Result.Fail("The end date can't be before the contract started.");

        contract.Status = ContractStatus.Ended;
        contract.EndedOn = endedOn;
        foreach (var l in contract.Properties.Where(p => p.IsActive)) { l.IsActive = false; l.RemovedOn = endedOn; }
        await db.SaveChangesAsync(ct);   // properties are free for a new contract; history stays for remittances
        return Result.Ok();
    }

    public async Task<IReadOnlyDictionary<int, (int OwnerId, string OwnerName, int ContractId)>> GetManagedAssetsAsync(CancellationToken ct = default)
        => (await db.ContractProperties.AsNoTracking().Where(p => p.IsActive)
                .Select(p => new { p.AssetId, p.Contract.OwnerId, OwnerName = p.Contract.Owner.FullName, p.ContractId })
                .ToListAsync(ct))
            .GroupBy(x => x.AssetId)
            .ToDictionary(g => g.Key, g => (g.First().OwnerId, g.First().OwnerName, g.First().ContractId));

    // ---------------------------------------------------------------------

    private async Task<string?> ValidateAsync(ContractInput input, CancellationToken ct)
    {
        var error = AgencyRules.ValidateContract(input.StartDate, input.EndDate, input.CommissionType, input.CommissionPercent, input.FixedMonthlyFee, input.RemittanceDay);
        if (error is not null) return error;
        if (!await db.PropertyOwners.AnyAsync(o => o.Id == input.OwnerId, ct)) return "Choose the owner.";
        if (input.AgentId is int a && !await db.Agents.AnyAsync(x => x.Id == a, ct)) return "Choose a valid agent.";
        return null;
    }

    private static void Apply(ManagementContract c, ContractInput i)
    {
        c.OwnerId = i.OwnerId;
        c.AgentId = i.AgentId;
        c.StartDate = i.StartDate;
        c.EndDate = i.EndDate;
        c.CommissionType = i.CommissionType;
        c.CommissionPercent = i.CommissionType == CommissionType.PercentOfCollections ? i.CommissionPercent : 0;
        c.FixedMonthlyFee = i.CommissionType == CommissionType.FixedMonthly ? i.FixedMonthlyFee : 0;
        c.RemittanceDay = i.RemittanceDay;
        c.Notes = i.Notes?.Trim();
    }
}
