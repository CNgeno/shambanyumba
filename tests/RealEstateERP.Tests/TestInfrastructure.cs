using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Tests;

public sealed class FakeCurrentUser : ICurrentUserService
{
    public bool IsAuthenticated => true;
    public string? UserId { get; set; } = "test-user";
    public int? BusinessId { get; set; }
    public BusinessRole? Role { get; set; } = BusinessRole.Owner;
    public BusinessType? BusinessType { get; set; } = RealEstateERP.Core.Enums.BusinessType.Owner;
    public bool IsSuperAdmin => false;
}

/// <summary>Stand-in tenant entity until real ones (Asset, Unit...) arrive in Sprint 2.</summary>
public sealed class Widget : TenantEntity
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUserService currentUser)
    : ApplicationDbContext(options, currentUser, TimeProvider.System)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder)
    {
        builder.Entity<Widget>();          // register first so the base class applies its filters to it
        base.OnModelCreating(builder);
    }

    public static (TestDbContext Db, FakeCurrentUser User) Create()
    {
        var user = new FakeCurrentUser();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))   // renewals use a transaction
            .Options;
        return (new TestDbContext(options, user), user);
    }
}
