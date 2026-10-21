using Microsoft.EntityFrameworkCore;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Queries_return_only_the_current_business_rows()
    {
        var (db, user) = TestDbContext.Create();

        user.BusinessId = 1;
        db.Widgets.Add(new Widget { Name = "A" });
        await db.SaveChangesAsync();

        user.BusinessId = 2;
        db.Widgets.Add(new Widget { Name = "B" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        user.BusinessId = 1;
        Assert.Equal(new[] { "A" }, await db.Widgets.Select(w => w.Name).ToListAsync());

        user.BusinessId = 2;
        Assert.Equal(new[] { "B" }, await db.Widgets.Select(w => w.Name).ToListAsync());
    }

    [Fact]
    public async Task No_selected_business_sees_nothing()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        db.Widgets.Add(new Widget { Name = "A" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        user.BusinessId = null;
        Assert.Empty(await db.Widgets.ToListAsync());
    }

    [Fact]
    public async Task BusinessId_is_stamped_automatically_on_create()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 7;

        var widget = new Widget { Name = "A" };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        Assert.Equal(7, widget.BusinessId);
        Assert.Equal("test-user", widget.CreatedBy);
        Assert.NotEqual(default, widget.CreatedAtUtc);
    }

    [Fact]
    public async Task Creating_tenant_data_without_a_business_throws()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = null;

        db.Widgets.Add(new Widget { Name = "orphan" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Writing_to_another_business_row_throws()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var widget = new Widget { Name = "A" };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        user.BusinessId = 2;
        widget.Name = "hijacked";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Remove_soft_deletes_and_hides_the_row()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var widget = new Widget { Name = "A" };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        db.Widgets.Remove(widget);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Empty(await db.Widgets.ToListAsync());

        // Turning off ONLY the soft-delete filter shows it again, still tenant-scoped.
        var deleted = await db.Widgets.IgnoreQueryFilters([QueryFilters.SoftDelete]).SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.Equal("test-user", deleted.DeletedBy);

        user.BusinessId = 2;
        Assert.Empty(await db.Widgets.IgnoreQueryFilters([QueryFilters.SoftDelete]).ToListAsync());
    }
}
