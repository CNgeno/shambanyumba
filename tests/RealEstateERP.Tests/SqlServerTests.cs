using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

/// <summary>
/// Runs only when SMARTBIZ_TEST_SQL holds a SQL Server connection string, e.g.
///   PowerShell: $env:SMARTBIZ_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
///   bash:       export SMARTBIZ_TEST_SQL="Server=localhost;User Id=sa;Password=...;TrustServerCertificate=True"
/// Otherwise these tests are reported as skipped.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTBIZ_TEST_SQL")))
            Skip = "Set SMARTBIZ_TEST_SQL to a SQL Server connection string to run the real-database tests.";
    }
}

/// <summary>
/// One throwaway database per test run: real migrations, a small data set across every module, dropped afterwards.
/// These tests catch what the in-memory provider can't: queries SQL Server can't translate, filtered unique indexes,
/// foreign keys, ExecuteUpdate.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    public FakeCurrentUser User { get; } = new();
    public FakeClock Clock { get; } = new(Now);
    public int BusinessId { get; private set; }
    public int AssetId { get; private set; }
    public int ContractId { get; private set; }
    public int ListingId { get; private set; }
    private string? _connection;

    public ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options, User, Clock);

    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("SMARTBIZ_TEST_SQL");
        if (string.IsNullOrWhiteSpace(raw)) return;   // tests are skipped
        _connection = new SqlConnectionStringBuilder(raw) { InitialCatalog = $"SmartBizTest_{Guid.NewGuid():N}" }.ConnectionString;

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        User.BusinessId = null;
        var business = new Business { Name = "SQL Test Estates", Type = BusinessType.Agency, CreditBalance = 100 };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        BusinessId = business.Id;
        var plan = new SubscriptionPlan { Code = "SQLTEST", Name = "SQL test", UnitLimit = 100 };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        db.Subscriptions.Add(new Subscription { BusinessId = business.Id, PlanId = plan.Id, PeriodStartUtc = Now.UtcDateTime.AddDays(-10), PeriodEndUtc = Now.UtcDateTime.AddDays(30) });
        await db.SaveChangesAsync();
        User.BusinessId = business.Id;
        User.BusinessType = BusinessType.Agency;
        User.UserId = "sql-tester";

        var asset = new Asset { Name = "SQL Court", Type = AssetType.Building, County = "Nairobi", Location = "Kilimani", AcquisitionCost = 10_000_000m };
        var let = new Unit { Asset = asset, UnitNumber = "A1", Type = UnitType.Apartment, Bedrooms = 2, MonthlyRent = 40_000m, Status = UnitStatus.Occupied, SizeSqm = 80 };
        var vacant = new Unit { Asset = asset, UnitNumber = "A2", Type = UnitType.Apartment, Bedrooms = 2, MonthlyRent = 40_000m, Status = UnitStatus.Available, SizeSqm = 80 };
        var plot = new Unit { Asset = asset, UnitNumber = "P1", Type = UnitType.Plot, SalePrice = 1_500_000m, Status = UnitStatus.Available, SizeSqm = 450 };
        var tenant = new Tenant { FullName = "Sql Tenant", Email = "t@sql.test" };
        var lease = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = let, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 40_000m, RentDueDay = 5 };
        var invoice = new Invoice { InvoiceNumber = "INV-1", Kind = InvoiceKind.Monthly, Tenant = tenant, Lease = lease, Status = InvoiceStatus.Pending,
            InvoiceDate = new DateOnly(2026, 9, 1), DueDate = new DateOnly(2026, 9, 5), PeriodStart = new DateOnly(2026, 9, 1), PeriodEnd = new DateOnly(2026, 9, 30),
            SubTotal = 40_000m, Total = 40_000m, AmountPaid = 30_000m };
        var payment = new Payment { ReceiptNumber = "RCT-1", Tenant = tenant, Lease = lease, PaymentDate = new DateOnly(2026, 9, 6), Amount = 30_000m, Method = PaymentMethod.Mpesa, Reference = "SQL1A2B3C4" };
        payment.Allocations.Add(new PaymentAllocation { Invoice = invoice, Amount = 30_000m });
        db.AddRange(asset, let, vacant, plot, tenant, lease, invoice, payment);

        var owner = new PropertyOwner { FullName = "Sql Owner", PayoutMethod = PayoutMethod.Bank, BankName = "Equity", BankAccountNumber = "01" };
        var agent = new Agent { FullName = "Sql Agent", CommissionSharePercent = 40 };
        var contract = new ManagementContract { ContractNumber = "MC-1", Owner = owner, Agent = agent, StartDate = new DateOnly(2026, 1, 1), Status = ContractStatus.Active, CommissionPercent = 10 };
        contract.Properties.Add(new ContractProperty { Asset = asset, AddedOn = new DateOnly(2026, 1, 1), IsActive = true });
        var buyer = new Buyer { FullName = "Sql Buyer" };
        var sale = new Sale { SaleNumber = "SL-1", Buyer = buyer, Unit = plot, SaleDate = new DateOnly(2026, 6, 1), Price = 1_500_000m, DepositAmount = 300_000m,
            PaymentPlan = PaymentPlan.Instalments, InstalmentCount = 2, Status = SaleStatus.Active };
        sale.Instalments.Add(new SaleInstalment { Number = 0, Description = "Deposit", DueDate = new DateOnly(2026, 6, 1), Amount = 300_000m, AmountPaid = 300_000m });
        sale.Instalments.Add(new SaleInstalment { Number = 1, Description = "Instalment 1 of 2", DueDate = new DateOnly(2026, 8, 1), Amount = 600_000m });
        sale.Instalments.Add(new SaleInstalment { Number = 2, Description = "Instalment 2 of 2", DueDate = new DateOnly(2026, 12, 1), Amount = 600_000m });
        sale.AmountPaid = 300_000m;
        var listing = new Listing { Unit = vacant, Type = ListingType.Rent, Title = "2-bedroom apartment in Kilimani", Description = new string('x', 60), Price = 40_000m,
            Status = ListingStatus.Published, PublishedAtUtc = Now.UtcDateTime };
        db.AddRange(owner, agent, contract, buyer, sale, listing,
            new ProjectCost { Asset = asset, Kind = CostKind.Budget, Category = CostCategory.Construction, Description = "Build", Amount = 20_000_000m },
            new ProjectCost { Asset = asset, Kind = CostKind.Actual, Category = CostCategory.Construction, Description = "Build", Amount = 21_000_000m, Date = new DateOnly(2025, 6, 1) });
        await db.SaveChangesAsync();
        AssetId = asset.Id;
        ContractId = contract.Id;
        ListingId = listing.Id;
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }
}

public class SqlServerTests(SqlServerFixture fx) : IClassFixture<SqlServerFixture>
{
    private SystemConfigService Config(ApplicationDbContext db) => new(db, new MemoryCache(new MemoryCacheOptions()));
    private AgentScopeService Scope(ApplicationDbContext db) => new(db, fx.User, new MemoryCache(new MemoryCacheOptions()));

    [SqlServerFact]
    public async Task Sales_lists_and_reports_translate()
    {
        await using var db = fx.NewContext();
        var sales = new SaleService(db, new NumberSequenceService(db), Scope(db), new FakeStorage(), Config(db), fx.User, fx.Clock);
        var list = await sales.ListAsync(SalesListView.All, null, null, null);    // the query that once failed on SQL Server
        var sale = Assert.Single(list);
        Assert.Equal(600_000m, sale.OverdueAmount);
        Assert.NotEmpty(await sales.GetInstalmentArrearsAsync());
        Assert.NotNull(await sales.GetTotalsAsync());
        Assert.NotEmpty(await sales.GetInventoryAsync());
    }

    [SqlServerFact]
    public async Task Business_reports_translate()
    {
        await using var db = fx.NewContext();
        var r = await new ReportService(db, fx.Clock).GetAsync(12);
        Assert.Equal(40_000m, r.Collections.Sum(c => c.Invoiced));
        Assert.Equal(30_000m, r.Collections.Sum(c => c.Collected));
        Assert.Contains(r.Vacant, v => v.UnitNumber == "A2");
        Assert.Single(r.ByProperty);
    }

    [SqlServerFact]
    public async Task Public_marketplace_translates_and_counts_views()
    {
        await using var db = fx.NewContext();
        var market = new MarketplaceService(db, new FakeEmail(), Config(db), fx.Clock);
        Assert.Single((await market.SearchAsync(new PublicSearch(ListingType.Rent, "Nairobi", "Kilimani", null, 50_000m, 2, UnitType.Apartment))).Items);
        Assert.NotNull(await market.GetAsync(fx.ListingId, countView: true));   // ExecuteUpdate: not supported in memory
        await using var check = fx.NewContext();
        Assert.Equal(1, (await check.Listings.SingleAsync(l => l.Id == fx.ListingId)).ViewCount);
    }

    [SqlServerFact]
    public async Task Agency_costing_arrears_and_leads_translate()
    {
        await using var db = fx.NewContext();
        var preview = await new RemittanceService(db, new NumberSequenceService(db), Scope(db), fx.Clock).PreviewAsync(fx.ContractId, 2026, 9);
        Assert.Equal(30_000m, preview!.Income);
        Assert.Single(await new AgencyService(db, new NumberSequenceService(db), Scope(db), fx.User, fx.Clock).ListContractsAsync(null, null));

        var overview = await new ProjectCostService(db, new FakeStorage(), Config(db), fx.Clock).GetOverviewAsync(fx.AssetId);
        Assert.Equal(31_000_000m, overview!.ExpectedTotal);   // land 10M + max(20M budget, 21M actual)

        Assert.NotEmpty((await new ArrearsService(db, new NumberSequenceService(db), Config(db), fx.Clock, fx.User).GetReportAsync(null)).Tenants);
        Assert.NotNull(await new ListingService(db, new FakeStorage(), Config(db), fx.User, fx.Clock).GetLeadsAsync(null));
        Assert.NotEmpty(await new ListingService(db, new FakeStorage(), Config(db), fx.User, fx.Clock).ListAsync(null));
    }

    [SqlServerFact]
    public async Task Filtered_unique_index_stops_a_second_open_listing()
    {
        await using var db = fx.NewContext();
        var unitId = (await db.Listings.AsNoTracking().SingleAsync(l => l.Id == fx.ListingId)).UnitId;
        db.Listings.Add(new Listing { UnitId = unitId, Type = ListingType.Rent, Title = "Duplicate listing title", Description = new string('y', 60), Price = 1, Status = ListingStatus.Draft });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
