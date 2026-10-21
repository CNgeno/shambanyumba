using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class RoleLabelTests
{
    [Theory]
    [InlineData(BusinessRole.Owner, BusinessType.Agency, false, "Principal")]
    [InlineData(BusinessRole.Owner, BusinessType.Owner, false, "Owner")]
    [InlineData(BusinessRole.User, BusinessType.Agency, true, "Agent")]
    [InlineData(BusinessRole.Admin, BusinessType.Agency, true, "Admin")]   // admins are never shown (or treated) as restricted agents
    [InlineData(BusinessRole.User, BusinessType.Agency, false, "User")]
    public void Labels(BusinessRole role, BusinessType type, bool linked, string expected)
        => Assert.Equal(expected, RoleLabels.For(role, type, linked));
}

public class AgentScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, MemoryCache Cache, FakeClock Clock,
        int JamesId, int WanjiruId, int JamesContract, int WanjiruContract, int OwnerA, int OwnerB)
    {
        public AgentScopeService Scope() => new(Db, User, Cache);
        public AgencyService Agency() => new(Db, new NumberSequenceService(Db), Scope(), User, Clock);
        public RemittanceService Remittances() => new(Db, new NumberSequenceService(Db), Scope(), Clock);

        public void SignInAs(string userId, BusinessRole role) { User.UserId = userId; User.Role = role; }
    }

    /// <summary>Two agents, each with an active contract for a different owner. James has a login; Wanjiru doesn't.</summary>
    private static async Task<Ctx> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        user.BusinessType = BusinessType.Agency;
        user.UserId = "principal";
        user.Role = BusinessRole.Owner;

        db.Users.AddRange(
            new ApplicationUser { Id = "principal", UserName = "amina@x.test", Email = "amina@x.test" },
            new ApplicationUser { Id = "james-login", UserName = "james@x.test", Email = "james@x.test" },
            new ApplicationUser { Id = "outsider", UserName = "out@x.test", Email = "out@x.test" });
        db.BusinessUsers.AddRange(
            new BusinessUser { BusinessId = 1, UserId = "principal", Role = BusinessRole.Owner },
            new BusinessUser { BusinessId = 1, UserId = "james-login", Role = BusinessRole.User });

        var james = new Agent { FullName = "James Kamau", CommissionSharePercent = 40m, UserId = "james-login" };
        var wanjiru = new Agent { FullName = "Wanjiru Njoroge", CommissionSharePercent = 35m };
        var ownerA = new PropertyOwner { FullName = "Grace Mwende" };
        var ownerB = new PropertyOwner { FullName = "Otieno Family Trust" };
        var a = new Asset { Name = "Kilimani Heights", Type = AssetType.Building };
        var b = new Asset { Name = "Westlands Court", Type = AssetType.Building };
        var c1 = new ManagementContract { ContractNumber = "MC-1", Owner = ownerA, Agent = james, StartDate = new DateOnly(2026, 1, 1), Status = ContractStatus.Active, CommissionPercent = 10 };
        var c2 = new ManagementContract { ContractNumber = "MC-2", Owner = ownerB, Agent = wanjiru, StartDate = new DateOnly(2026, 1, 1), Status = ContractStatus.Active, CommissionPercent = 8 };
        c1.Properties.Add(new ContractProperty { Asset = a, AddedOn = new DateOnly(2026, 1, 1), IsActive = true });
        c2.Properties.Add(new ContractProperty { Asset = b, AddedOn = new DateOnly(2026, 1, 1), IsActive = true });
        db.AddRange(james, wanjiru, ownerA, ownerB, a, b, c1, c2);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new MemoryCache(new MemoryCacheOptions()), new FakeClock(Now), james.Id, wanjiru.Id, c1.Id, c2.Id, ownerA.Id, ownerB.Id);
    }

    [Fact]
    public async Task A_linked_agent_sees_only_their_own_contracts_and_owners()
    {
        var c = await SetupAsync();
        c.SignInAs("james-login", BusinessRole.User);

        var scope = await c.Scope().GetAsync();
        Assert.True(scope.IsRestricted);
        Assert.Equal(c.JamesId, scope.AgentId);

        Assert.Equal(new[] { c.JamesContract }, (await c.Agency().ListContractsAsync(null, null)).Select(x => x.Id));
        Assert.Null(await c.Agency().GetContractAsync(c.WanjiruContract));
        Assert.Equal(new[] { c.OwnerA }, (await c.Agency().ListOwnersAsync(null)).Select(o => o.Id));
        Assert.Null(await c.Agency().GetOwnerAsync(c.OwnerB));
        Assert.Single(await c.Agency().ListAgentsAsync(true));   // only himself
    }

    [Fact]
    public async Task An_agent_can_prepare_but_not_manage()
    {
        var c = await SetupAsync();
        c.SignInAs("james-login", BusinessRole.User);
        var terms = new ContractInput(c.OwnerA, c.JamesId, new DateOnly(2026, 1, 1), null, CommissionType.PercentOfCollections, 50, 0, 10, null);

        Assert.False((await c.Agency().UpdateContractAsync(c.JamesContract, terms)).Succeeded);   // can't raise his own commission
        Assert.False((await c.Agency().EndAsync(c.JamesContract, new DateOnly(2026, 9, 30))).Succeeded);
        Assert.False((await c.Agency().CreateContractAsync(terms)).Succeeded);

        var own = await c.Remittances().CreateAsync(c.JamesContract, 2026, 9);
        Assert.True(own.Succeeded, own.Error);
        Assert.False((await c.Remittances().CreateAsync(c.WanjiruContract, 2026, 9)).Succeeded);   // not his
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Remittances().AddAdjustmentAsync(own.Value, "Repair", -500m)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Remittances().ApproveAsync(own.Value)).Succeeded);
    }

    [Fact]
    public async Task The_principal_and_admins_see_everything_even_if_linked()
    {
        var c = await SetupAsync();
        Assert.Equal(2, (await c.Agency().ListContractsAsync(null, null)).Count);   // principal

        c.SignInAs("james-login", BusinessRole.Admin);
        Assert.False((await c.Scope().GetAsync()).IsRestricted);
        Assert.Equal(2, (await c.Agency().ListContractsAsync(null, null)).Count);
    }

    [Fact]
    public async Task Linking_requires_a_member_and_one_agent_per_login()
    {
        var c = await SetupAsync();
        AgentInput Input(string? userId) => new("Wanjiru Njoroge", null, null, null, 35m, true, userId);

        Assert.False((await c.Agency().SaveAgentAsync(c.WanjiruId, Input("outsider"))).Succeeded);      // not in the business
        Assert.False((await c.Agency().SaveAgentAsync(c.WanjiruId, Input("james-login"))).Succeeded);   // already James's
    }

    [Fact]
    public async Task Unlinking_takes_effect_immediately()
    {
        var c = await SetupAsync();
        c.SignInAs("james-login", BusinessRole.User);
        Assert.True((await c.Scope().GetAsync()).IsRestricted);   // now cached

        c.SignInAs("principal", BusinessRole.Owner);
        Assert.True((await c.Agency().SaveAgentAsync(c.JamesId, new AgentInput("James Kamau", null, null, null, 40m, true, null))).Succeeded);

        c.SignInAs("james-login", BusinessRole.User);
        Assert.Null((await c.Scope().GetAsync()).AgentId);   // cache was cleared on save
    }

    [Fact]
    public async Task Principal_can_move_an_agents_contracts_to_another()
    {
        var c = await SetupAsync();
        var moved = await c.Agency().ReassignContractsAsync(c.JamesId, c.WanjiruId);
        Assert.Equal(1, moved.Value);
        Assert.Equal(c.WanjiruId, (await c.Db.ManagementContracts.SingleAsync(x => x.Id == c.JamesContract)).AgentId);

        c.SignInAs("james-login", BusinessRole.User);
        Assert.Empty(await c.Agency().ListContractsAsync(null, null));
        Assert.False((await c.Agency().ReassignContractsAsync(c.WanjiruId, c.JamesId)).Succeeded);   // agents can't
    }
}
