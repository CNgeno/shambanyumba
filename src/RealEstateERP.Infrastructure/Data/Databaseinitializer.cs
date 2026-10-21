using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>
/// Everything the app does to the database at startup, in one place:
/// 1. Development, or anywhere with Database:MigrateOnStartup = true: check the database is safe to migrate,
///    then apply pending migrations (EF Core takes a lock, so several instances can't migrate at once).
///    Otherwise, pending migrations are logged as critical (and /health reports Degraded).
/// 2. Always: idempotent seeding (plans, settings, roles), after migrating so the schema is current.
/// 3. Development only: demo users and businesses.
/// </summary>
public static class DatabaseInitializer
{
    private const string EfFlags = "--project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web";

    public static async Task InitializeAsync(IServiceProvider services, IHostEnvironment env, IConfiguration config, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName!);
        var db = sp.GetRequiredService<ApplicationDbContext>();

        // Production default: migrations are a deployment step (dotnet ef migrations script --idempotent).
        // Opt in to applying them at startup with Database:MigrateOnStartup = true (App Service: Database__MigrateOnStartup).
        // On by default (Version 1): every launch applies pending migrations before seeding. Turn off with Database__MigrateOnStartup=false.
        if (env.IsDevelopment() || config.GetValue("Database:MigrateOnStartup", true))
        {
            var timeout = config.GetValue("Database:MigrationTimeoutSeconds", 300);
            db.Database.SetCommandTimeout(TimeSpan.FromSeconds(Math.Max(30, timeout)));   // big schema changes can outlast the default 30s
            await MigrateSafelyAsync(db, logger, ct);
            db.Database.SetCommandTimeout((int?)null);   // back to the connection default
        }
        else
        {
            await WarnIfPendingAsync(db, logger, ct);
        }

        await DataSeeder.SeedAsync(sp, config);

        if (env.IsDevelopment())
            await DemoDataSeeder.SeedAsync(sp, config);
    }

    private static async Task MigrateSafelyAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        StopIfModelHasNoMigration(db, logger);
        var known = db.Database.GetMigrations().ToList();
        if (known.Count == 0)
            throw new InvalidOperationException(
                "No EF Core migrations exist yet. Create the first one with:" + Environment.NewLine +
                $"  dotnet ef migrations add InitialFoundation {EfFlags} --output-dir Data/Migrations");

        // CanConnect is false when the database doesn't exist yet; MigrateAsync will create it.
        if (await db.Database.CanConnectAsync(ct))
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();

            if (applied.Count == 0 && await CountTablesAsync(db, ct) > 0)
                throw new InvalidOperationException(Explain(db,
                    "already contains tables that were not created by this project's migrations " +
                    "(usually the database of an earlier version of the project)."));

            var unknown = applied.Except(known).ToList();
            if (unknown.Count > 0)
                throw new InvalidOperationException(Explain(db,
                    $"has migrations applied that this project doesn't contain ({string.Join(", ", unknown)}). " +
                    "This usually means the Migrations folder was deleted and recreated."));
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database {Database} is up to date.", db.Database.GetDbConnection().Database);
            return;
        }

        logger.LogInformation("Applying migrations to {Database}: {Migrations}",
            db.Database.GetDbConnection().Database, string.Join(", ", pending));
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// The code's data model has changes that no migration creates (a migration was forgotten before publishing).
    /// Stop with the exact fix instead of a stack trace; running anyway would fail later with "Invalid column name".
    /// </summary>
    private static void StopIfModelHasNoMigration(ApplicationDbContext db, ILogger logger)
    {
        if (!db.Database.HasPendingModelChanges()) return;
        logger.LogCritical(
            "The code has database changes without a migration, so the database can't be updated safely. On a development machine run:\n" +
            "  dotnet ef migrations add <Name> {Flags} --output-dir Data/Migrations\n" +
            "check the new migration, rebuild and redeploy. (The 'Model matches the migrations' test catches this before publishing.)", EfFlags);
        throw new InvalidOperationException("Pending model changes: add a migration (dotnet ef migrations add <Name>) and redeploy. See the log above.");
    }

    /// <summary>Migrations not applied and not allowed to apply: say so loudly with the exact fix.</summary>
    private static async Task WarnIfPendingAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0) return;
            logger.LogCritical(
                "Database {Database} is missing {Count} migration(s): {Migrations}. Pages using new tables will fail. " +
                "Apply them with the idempotent script (dotnet ef migrations script --idempotent {Flags} -o migrate.sql) " +
                "or set Database__MigrateOnStartup=true and restart.",
                db.Database.GetDbConnection().Database, pending.Count, string.Join(", ", pending), EfFlags);
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            logger.LogCritical(ex, "Could not check the database for pending migrations.");
        }
    }

    private static async Task<int> CountTablesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var result = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory'")
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    private static string Explain(ApplicationDbContext db, string problem)
    {
        var name = db.Database.GetDbConnection().Database;
        return $"Database '{name}' {problem}" + Environment.NewLine +
               "Fix, either:" + Environment.NewLine +
               $"  - point ConnectionStrings:DefaultConnection at a new database (e.g. {name}_v2), or" + Environment.NewLine +
               "  - if nothing in it is needed, drop it and run again:" + Environment.NewLine +
               $"    dotnet ef database drop {EfFlags}";
    }
}
