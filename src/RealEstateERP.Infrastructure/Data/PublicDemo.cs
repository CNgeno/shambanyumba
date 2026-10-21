using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstateERP.Core.Constants;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>
/// The public demo: two logins (owner, agent) and three demo businesses. EnsureAsync creates whatever is missing;
/// ResetAsync deletes everything belonging to demo businesses (SQL Server) so EnsureAsync can seed a clean copy.
/// </summary>
public static class PublicDemo
{
    public static async Task<(ApplicationUser Owner, ApplicationUser Agent)> EnsureAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();
        var owner = await EnsureUserAsync(users, DemoUsers.Owner, "Demo Visitor");
        var agent = await EnsureUserAsync(users, DemoUsers.Agent, "James Kamau (demo agent)");
        await DemoDataSeeder.SeedPublicDemoAsync(db, owner.Id, agent.Id);
        return (owner, agent);
    }

    /// <summary>Deletes all rows of demo businesses, across every table with a BusinessId, in one transaction.</summary>
    public static async Task ResetAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @sql nvarchar(max) = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL;'
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = N'BusinessId') OR t.name = N'Businesses';
            EXEC sp_executesql @sql;

            SET @sql = N'';
            SELECT @sql += N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                         + N' WHERE BusinessId IN (SELECT Id FROM dbo.Businesses WHERE IsDemo = 1);'
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'BusinessId'
            WHERE t.name <> N'Businesses';
            EXEC sp_executesql @sql;
            DELETE FROM dbo.Businesses WHERE IsDemo = 1;

            SET @sql = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = N'BusinessId') OR t.name = N'Businesses';
            EXEC sp_executesql @sql;
            COMMIT TRANSACTION;
            """;
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> users, string email, string name)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name };
        // A long random password nobody knows: demo logins only sign in through the one-click button.
        var result = await users.CreateAsync(user, $"Dm!{Guid.NewGuid():N}aA1");
        if (!result.Succeeded) throw new InvalidOperationException("Couldn't create the demo login: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }
}
