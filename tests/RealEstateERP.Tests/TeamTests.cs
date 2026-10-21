using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public sealed class FakeEmail : IEmailService
{
    public List<EmailMessage> Sent { get; } = [];
    public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    public string LastToken() => Regex.Match(Sent[^1].HtmlBody, "token=([0-9A-F]+)").Groups[1].Value;
}

public class EmailTemplateTests
{
    [Fact]
    public void Text_is_html_encoded()
    {
        var html = EmailTemplates.Render("SmartBiz", "<script>x</script>", ["Tom & Jerry"], "Go", "https://example.test/?a=1&b=2");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Tom &amp; Jerry", html);
        Assert.Contains("a=1&amp;b=2", html);
    }
}

public class TeamServiceTests
{
    private const string Url = "https://smartbiz.test/Invitations/Accept?token={0}";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeEmail Email, FakeClock Clock, int BusinessId)
    {
        public TeamService Team() => new(Db, Email, new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), User, Clock);

        public async Task<string> AddUserAsync(string id, string email, bool confirmed = true)
        {
            Db.Users.Add(new ApplicationUser { Id = id, UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant(), EmailConfirmed = confirmed });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task<int> AddMemberAsync(string userId, BusinessRole role)
        {
            var m = new BusinessUser { BusinessId = BusinessId, UserId = userId, Role = role };
            Db.BusinessUsers.Add(m);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return m.Id;
        }

        public void ActAs(string userId, BusinessRole role) { User.UserId = userId; User.Role = role; }
    }

    private static async Task<Ctx> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Acacia Gardens Ltd" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;
        var c = new Ctx(db, user, new FakeEmail(), new FakeClock(Now), business.Id);
        await c.AddUserAsync("owner", "owner@smartbiz.test");
        await c.AddMemberAsync("owner", BusinessRole.Owner);
        c.ActAs("owner", BusinessRole.Owner);
        return c;
    }

    [Fact]
    public async Task Invitation_email_can_be_accepted_once_by_the_invited_address()
    {
        var c = await SetupAsync();
        Assert.True((await c.Team().InviteAsync("Faith@SmartBiz.test", BusinessRole.User, Url)).Succeeded);
        var token = c.Email.LastToken();
        Assert.Equal("faith@smartbiz.test", c.Email.Sent[^1].To);

        await c.AddUserAsync("faith", "faith@smartbiz.test");
        var accepted = await c.Team().AcceptByTokenAsync(token, "faith");
        Assert.True(accepted.Succeeded, accepted.Error);
        c.Db.ChangeTracker.Clear();

        var membership = await c.Db.BusinessUsers.SingleAsync(m => m.UserId == "faith");
        Assert.Equal(BusinessRole.User, membership.Role);
        Assert.False((await c.Team().AcceptByTokenAsync(token, "faith")).Succeeded);   // used
    }

    [Fact]
    public async Task Someone_else_cannot_use_the_invitation()
    {
        var c = await SetupAsync();
        await c.Team().InviteAsync("faith@smartbiz.test", BusinessRole.Admin, Url);
        await c.AddUserAsync("intruder", "intruder@smartbiz.test");

        var result = await c.Team().AcceptByTokenAsync(c.Email.LastToken(), "intruder");
        Assert.False(result.Succeeded);
        Assert.Contains("faith@smartbiz.test", result.Error);
        Assert.False(await c.Db.BusinessUsers.AnyAsync(m => m.UserId == "intruder"));
    }

    [Fact]
    public async Task Unconfirmed_accounts_cannot_accept()
    {
        var c = await SetupAsync();
        await c.Team().InviteAsync("faith@smartbiz.test", BusinessRole.User, Url);
        await c.AddUserAsync("faith", "faith@smartbiz.test", confirmed: false);
        Assert.False((await c.Team().AcceptByTokenAsync(c.Email.LastToken(), "faith")).Succeeded);
    }

    [Fact]
    public async Task Resend_replaces_the_link_and_revoke_closes_it()
    {
        var c = await SetupAsync();
        await c.Team().InviteAsync("faith@smartbiz.test", BusinessRole.User, Url);
        var first = c.Email.LastToken();
        Assert.False((await c.Team().InviteAsync("faith@smartbiz.test", BusinessRole.User, Url)).Succeeded);   // already open

        var id = (await c.Db.BusinessInvitations.SingleAsync()).Id;
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Team().ResendInvitationAsync(id, Url)).Succeeded);
        var second = c.Email.LastToken();
        Assert.NotEqual(first, second);
        Assert.Null(await c.Team().DescribeTokenAsync(first));

        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Team().RevokeInvitationAsync(id)).Succeeded);
        Assert.False((await c.Team().DescribeTokenAsync(second))!.IsOpen);
    }

    [Fact]
    public async Task A_business_always_keeps_an_owner()
    {
        var c = await SetupAsync();
        var ownerMembership = (await c.Db.BusinessUsers.SingleAsync(m => m.UserId == "owner")).Id;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Team().ChangeRoleAsync(ownerMembership, BusinessRole.Admin)).Succeeded);
        Assert.False((await c.Team().RemoveAsync(ownerMembership)).Succeeded);

        await c.AddUserAsync("second", "second@smartbiz.test");
        await c.AddMemberAsync("second", BusinessRole.Owner);
        Assert.True((await c.Team().ChangeRoleAsync(ownerMembership, BusinessRole.Admin)).Succeeded);
    }

    [Fact]
    public async Task Admins_cannot_touch_owners()
    {
        var c = await SetupAsync();
        await c.AddUserAsync("admin", "admin@smartbiz.test");
        await c.AddMemberAsync("admin", BusinessRole.Admin);
        await c.AddUserAsync("staff", "staff@smartbiz.test");
        var staff = await c.AddMemberAsync("staff", BusinessRole.User);
        var owner = (await c.Db.BusinessUsers.SingleAsync(m => m.UserId == "owner")).Id;
        c.Db.ChangeTracker.Clear();
        c.ActAs("admin", BusinessRole.Admin);

        Assert.False((await c.Team().InviteAsync("boss@smartbiz.test", BusinessRole.Owner, Url)).Succeeded);
        Assert.False((await c.Team().RemoveAsync(owner)).Succeeded);
        Assert.False((await c.Team().ChangeRoleAsync(staff, BusinessRole.Owner)).Succeeded);
        Assert.True((await c.Team().ChangeRoleAsync(staff, BusinessRole.Viewer)).Succeeded);
    }

    [Fact]
    public async Task Pending_invitations_show_for_the_invitee()
    {
        var c = await SetupAsync();
        await c.Team().InviteAsync("faith@smartbiz.test", BusinessRole.User, Url);
        await c.AddUserAsync("faith", "faith@smartbiz.test");

        var pending = Assert.Single(await c.Team().GetInvitationsForUserAsync("faith"));
        Assert.Equal("Acacia Gardens Ltd", pending.BusinessName);
        Assert.True((await c.Team().AcceptByIdAsync(pending.Id, "faith")).Succeeded);
        Assert.Empty(await c.Team().GetInvitationsForUserAsync("faith"));
    }
}
