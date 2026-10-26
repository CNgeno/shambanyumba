using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Tests;

/// <summary>
/// Fails when the data model has changed but no migration was added, the mistake that otherwise only shows up when
/// the app starts in production ("PendingModelChangesWarning"). Needs no database: it compares the model with the
/// migrations' snapshot. Fix: dotnet ef migrations add &lt;Name&gt; --project src/RealEstateERP.Infrastructure
/// --startup-project src/RealEstateERP.Web --output-dir Data/Migrations
/// </summary>
public class ModelMigrationTests
{
    [Fact]
    public void Model_matches_the_migrations()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelCheckOnly;Trusted_Connection=True")   // never opened
            // AddDefaultIdentity in Program.cs limits Identity's key columns to 128 characters, and the migrations were
            // made with that; without it Identity assumes 450 and this test reports changes that aren't there.
            .UseApplicationServiceProvider(new ServiceCollection()
                .Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128)
                .BuildServiceProvider())
            .Options;
        using var db = new ApplicationDbContext(options, new FakeCurrentUser(), TimeProvider.System);
        Assert.False(db.Database.HasPendingModelChanges(),
            "The model has changes without a migration. Run: dotnet ef migrations add <Name> --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations");
    }
}
