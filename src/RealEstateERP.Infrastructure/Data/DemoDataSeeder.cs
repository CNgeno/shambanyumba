using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Common;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>
/// DEVELOPMENT ONLY (Program.cs checks the environment, this checks DemoData:Enabled).
/// Creates demo users and businesses covering every role and every subscription state.
/// Dates are relative to the first run: drop the database to re-seed fresh states.
/// </summary>
public static class DemoDataSeeder
{
    private const string MarkerBusiness = "Acacia Gardens Ltd";

    public static async Task SeedAsync(IServiceProvider scopedServices, IConfiguration config)
    {
        if (!config.GetValue<bool>("DemoData:Enabled"))
            return;

        var password = config["DemoData:Password"] is { Length: > 0 } p ? p : "Demo@12345";
        var users = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();

        // Users are ensured every run (idempotent).
        var ids = new Dictionary<string, string>();
        foreach (var account in DemoAccounts.All)
        {
            var user = await EnsureUserAsync(users, account, password);
            ids[account.Email] = user.Id;
            if (account.Email == DemoAccounts.SuperAdmin && !await users.IsInRoleAsync(user, AppRoles.SuperAdmin))
                await users.AddToRoleAsync(user, AppRoles.SuperAdmin);
        }

        // Businesses only once.
        if (await db.Businesses.AnyAsync(b => b.Name == MarkerBusiness))
        {
            await SeedPortfolioAsync(db);   // Sprint 2: adds properties to databases seeded earlier
            await SeedTenanciesAsync(db);   // Sprint 3: tenants and leases
            await SeedBillingAsync(db);     // Sprint 4: charges, readings, last month's invoices
            await SeedCollectionsAsync(db); // Sprint 5: payments, a reversal, a deposit refund
            await SeedArrearsAsync(db);     // Sprint 6: credits and collections history
            await SeedAgencyAsync(db);      // Sprint 8: Mji Property Agents (owners, agents, contracts)
            await LinkAgentLoginAsync(db, ids);   // James Kamau signs in as an agent
            await SeedSalesAsync(db);             // Sprint 9: Savanna Homes plot sales
            await SeedCostingAsync(db);           // Development costing for Savanna Heights and Acacia Court
            await SeedListingsAsync(db);          // Sprint 10: public listings and verified leads
            return;
        }

        var plans = await db.SubscriptionPlans.ToDictionaryAsync(x => x.Code);
        var now = DateTime.UtcNow;

        // 1. Acacia Gardens: on trial, 11 days left. Has all four roles.
        var acacia = NewBusiness(MarkerBusiness, BusinessType.Owner, "P051234567A",
            (ids[DemoAccounts.Owner], BusinessRole.Owner),
            (ids[DemoAccounts.Admin], BusinessRole.Admin),
            (ids[DemoAccounts.Staff], BusinessRole.User),
            (ids[DemoAccounts.Viewer], BusinessRole.Viewer));
        AddTrial(db, acacia, plans["PROFESSIONAL"], start: now.AddDays(-3));

        // 2. Savanna Homes: paid Professional, ends in 4 days (shows the "ending soon" banner).
        var savanna = NewBusiness("Savanna Homes Developers", BusinessType.Developer, "P059876543B",
            (ids[DemoAccounts.Owner], BusinessRole.Owner));
        var paidEnd = now.AddDays(4);
        var paidStart = paidEnd.AddMonths(-1);
        AddTrial(db, savanna, plans["PROFESSIONAL"], start: paidStart.AddDays(-14));
        var savannaPayment = new SubscriptionPayment
        {
            Business = savanna, PlanId = plans["PROFESSIONAL"].Id, Months = 1, Amount = plans["PROFESSIONAL"].MonthlyFee,
            Method = SubscriptionPaymentMethod.Mpesa, Reference = "DEMOPAID01",
            Status = PaymentVerificationStatus.Approved, ReviewedByUserId = ids[DemoAccounts.SuperAdmin],
            ReviewedAtUtc = paidStart, ReviewNote = "Demo: verified on M-PESA statement"
        };
        db.SubscriptionPayments.Add(savannaPayment);
        db.Subscriptions.Add(new Subscription
        {
            Business = savanna, PlanId = plans["PROFESSIONAL"].Id, PeriodStartUtc = paidStart, PeriodEndUtc = paidEnd,
            IsTrial = false, SourcePayment = savannaPayment
        });

        // 3. Mji Property Agents: trial ended yesterday -> Grace.
        var mji = NewBusiness("Mji Property Agents", BusinessType.Agency, "P052468135C",
            (ids[DemoAccounts.Agent], BusinessRole.Owner));
        AddTrial(db, mji, plans["AGENT"], start: now.AddDays(-15));

        // 4. Lapsed Holdings: trial ended 26 days ago -> Suspended, with a payment waiting for approval.
        var lapsed = NewBusiness("Lapsed Holdings", BusinessType.Owner, null,
            (ids[DemoAccounts.Lapsed], BusinessRole.Owner));
        AddTrial(db, lapsed, plans["PROFESSIONAL"], start: now.AddDays(-40));
        var pending = new SubscriptionPayment
        {
            Business = lapsed, PlanId = plans["STARTER"].Id, Months = 1, Amount = plans["STARTER"].MonthlyFee,
            Method = SubscriptionPaymentMethod.Mpesa, Reference = "DEMOPEND01", Status = PaymentVerificationStatus.Pending
        };
        db.SubscriptionPayments.Add(pending);

        db.Businesses.AddRange(acacia, savanna, mji, lapsed);
        await db.SaveChangesAsync();

        // Back-date submission times (audit stamping sets "now" on insert).
        await db.SubscriptionPayments.Where(x => x.Id == savannaPayment.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, paidStart.AddHours(-3)));
        await db.SubscriptionPayments.Where(x => x.Id == pending.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, now.AddDays(-2)));

        await SeedPortfolioAsync(db);
        await SeedTenanciesAsync(db);
        await SeedBillingAsync(db);
        await SeedCollectionsAsync(db);
        await SeedArrearsAsync(db);
        await SeedAgencyAsync(db);
        await LinkAgentLoginAsync(db, ids);
        await SeedSalesAsync(db);
        await SeedCostingAsync(db);
        await SeedListingsAsync(db);
    }

    // ------------------------------------------------------------------ public demo

    /// <summary>
    /// The public demo (any environment without the development seed): Acacia Gardens (an estate to let), Savanna
    /// Homes (land and plot sales) and Mji Property Agents (an agency), all owned by the one-click demo login, with
    /// the demo agent linked to James Kamau. Same journeys as the development seed, no other accounts.
    /// </summary>
    public static async Task SeedPublicDemoAsync(ApplicationDbContext db, string demoUserId, string agentUserId)
    {
        if (await db.Businesses.AnyAsync(b => b.IsDemo)) return;
        // The development seed uses the same names; never mix the two.
        if (await db.Businesses.AnyAsync(b => b.Name == MarkerBusiness)) return;

        var plans = await db.SubscriptionPlans.ToDictionaryAsync(x => x.Code);
        var plan = plans.GetValueOrDefault("ENTERPRISE") ?? plans.Values.OrderByDescending(p => p.MonthlyFee).First();
        var now = DateTime.UtcNow;
        Business Demo(string name, BusinessType type, string kra)
        {
            var b = NewBusiness(name, type, kra, (demoUserId, BusinessRole.Owner));
            b.IsDemo = true;
            b.CreditBalance = 100_000;   // never runs out
            db.Subscriptions.Add(new Subscription { Business = b, PlanId = plan.Id, PeriodStartUtc = now.AddDays(-30), PeriodEndUtc = now.AddYears(10) });
            return b;
        }
        db.Businesses.AddRange(Demo(MarkerBusiness, BusinessType.Owner, "P051234567A"),
            Demo("Savanna Homes Developers", BusinessType.Developer, "P059876543B"),
            Demo("Mji Property Agents", BusinessType.Agency, "P052468135C"));
        await db.SaveChangesAsync();

        await SeedPortfolioAsync(db);
        await SeedTenanciesAsync(db);
        await SeedBillingAsync(db);
        await SeedCollectionsAsync(db);
        await SeedArrearsAsync(db);
        await SeedAgencyAsync(db);
        await LinkAgentLoginAsync(db, new Dictionary<string, string> { [DemoAccounts.James] = agentUserId });
        await SeedSalesAsync(db);
        await SeedCostingAsync(db);
        await SeedListingsAsync(db);
    }

    // ------------------------------------------------------------------ Sprint 10: marketplace

    /// <summary>
    /// Public listings for vacant flats (Acacia Court), plots (Savanna Heights) and an agency flat (Kilimani Heights),
    /// plus verified leads: one paid lead at Acacia, one at Savanna already at viewing stage, one waiting for credits.
    /// </summary>
    private static async Task SeedListingsAsync(ApplicationDbContext db)
    {
        if (await db.Listings.IgnoreQueryFilters().AnyAsync()) return;
        var now = DateTime.UtcNow;

        async Task<List<Unit>> AvailableAsync(string assetName, int take)
            => await db.Units.IgnoreQueryFilters()
                .Where(u => u.Asset.Name == assetName && u.Status == UnitStatus.Available && !u.IsDeleted)
                .OrderBy(u => u.Id).Take(take).Include(u => u.Asset).ToListAsync();

        Listing List(Unit u, ListingType type, string title, string description, decimal price, int daysAgo)
            => new()
            {
                BusinessId = u.BusinessId, UnitId = u.Id, Type = type, Title = title, Description = description, Price = price,
                Status = ListingStatus.Published, PublishedAtUtc = now.AddDays(-daysAgo), ViewCount = 20 + daysAgo * 7
            };

        var listings = new List<Listing>();
        foreach (var (u, i) in (await AvailableAsync("Acacia Court", 2)).Select((u, i) => (u, i)))
            listings.Add(List(u, ListingType.Rent, $"{(u.Bedrooms is int b and > 0 ? $"{b}-bedroom" : "Spacious")} apartment in Ruaka, Acacia Court {u.UnitNumber}",
                "Bright, secure apartment off Limuru Road, 10 minutes from Two Rivers and the Northern Bypass. Borehole and county water, " +
                "backup generator for common areas, ample parking and a caretaker on site. Garbage collection and security included in the service charge.",
                u.MonthlyRent > 0 ? u.MonthlyRent : 28_000m, 3 + i * 4));
        foreach (var (u, i) in (await AvailableAsync("Savanna Heights", 3)).Select((u, i) => (u, i)))
            listings.Add(List(u, ListingType.Sale, $"{u.SizeSqm ?? 450:0}sqm plot in Kitengela, Savanna Heights {u.UnitNumber}",
                "Ready title deed plot in a gated, beaconed scheme 2km off Namanga Road. Murram access roads, water on site and power " +
                "coming this quarter. Flexible payment: 20% deposit and up to 12 monthly instalments. Free site visits every Saturday.",
                u.SalePrice ?? 1_450_000m, 2 + i * 5));
        foreach (var u in await AvailableAsync("Kilimani Heights", 1))
            listings.Add(List(u, ListingType.Rent, $"2-bedroom apartment in Kilimani, Kilimani Heights {u.UnitNumber}",
                "Modern 2-bedroom, 2-bathroom apartment on Argwings Kodhek Road, close to Yaya Centre. Lift, backup generator, " +
                "borehole, gym and 24-hour security. Managed by Mji Property Agents.", u.MonthlyRent > 0 ? u.MonthlyRent : 45_000m, 6));
        if (listings.Count == 0) return;
        db.Listings.AddRange(listings);
        await db.SaveChangesAsync();

        ListingLead Lead(Listing l, string name, string phone, string email, string message, LeadStatus status, LeadStage stage, int hoursAgo, int credits)
            => new()
            {
                BusinessId = l.BusinessId, ListingId = l.Id, Name = name, Phone = phone, Email = email, Message = message,
                ConsentedAtUtc = now.AddHours(-hoursAgo), VerifyBeforeUtc = now.AddHours(48 - hoursAgo), VerifiedAtUtc = now.AddHours(-hoursAgo + 1),
                Status = status, Stage = stage, CreditsCharged = credits, UnlockedAtUtc = status == LeadStatus.Unlocked ? now.AddHours(-hoursAgo + 1) : null
            };

        var rent = listings.FirstOrDefault(l => l.Type == ListingType.Rent && l.Title.Contains("Ruaka"));
        var plot = listings.FirstOrDefault(l => l.Type == ListingType.Sale);
        var leads = new List<ListingLead>();
        if (rent is not null)
        {
            leads.Add(Lead(rent, "Brian Otieno", "+254711445566", "brian.otieno@example.test", "Is it available from next month? I work in Gigiri.", LeadStatus.Unlocked, LeadStage.New, 5, 10));
            leads.Add(Lead(rent, "Mary Wanjiru", "+254722556677", "mary.w@example.test", "Can I view it on Saturday?", LeadStatus.AwaitingCredits, LeadStage.New, 2, 0));
        }
        if (plot is not null)
            leads.Add(Lead(plot, "Peter Kariuki", "+254733667788", "pkariuki@example.test", "Interested in two adjacent plots. Is there a discount for cash?", LeadStatus.Unlocked, LeadStage.Viewing, 50, 10));
        db.ListingLeads.AddRange(leads);

        // Charge the paid demo leads through the ledger so balances stay consistent.
        foreach (var group in leads.Where(l => l.CreditsCharged > 0).GroupBy(l => l.BusinessId))
        {
            var business = await db.Businesses.FirstAsync(b => b.Id == group.Key);
            foreach (var l in group)
                db.CreditTransactions.Add(CreditLedger.Post(business, -l.CreditsCharged, CreditTransactionType.Usage, "Lead (demo)", null));
        }
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Development costing

    /// <summary>
    /// Savanna Heights: 1.6 acres bought for 8M (8.5M budgeted), subdivided into 12 plots; subdivision ran over
    /// budget, roads and water are in, power is still to come. Acacia Court: flats built to let, construction 8%
    /// over budget, with a year of letting costs.
    /// </summary>
    private static async Task SeedCostingAsync(ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var heights = await db.Assets.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Name == "Savanna Heights" && !a.IsDeleted);
        var court = await db.Assets.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Name == MarkerAsset && !a.IsDeleted);
        if (heights is null || court is null) return;
        if (await db.ProjectCosts.IgnoreQueryFilters().AnyAsync(c => c.AssetId == heights.Id || c.AssetId == court.Id)) return;

        ProjectCost Cost(Asset asset, CostKind kind, CostCategory category, string description, decimal amount, int? monthsAgo = null, string? supplier = null)
            => new()
            {
                BusinessId = asset.BusinessId, AssetId = asset.Id, Kind = kind, Category = category, Description = description, Amount = amount,
                Date = monthsAgo is int m ? today.AddMonths(-m) : null, Supplier = supplier
            };

        // ---- Savanna Heights: plots for sale ----
        heights.LandSizeAcres = 1.6m;
        heights.AcquisitionCost = 8_000_000m;
        heights.AcquisitionDate = today.AddMonths(-11);
        heights.SellerName = "Ole Sankale family";
        heights.EstimatedLandCost = 8_500_000m;
        heights.CostAllocation = CostAllocationMethod.BySize;
        heights.SubdivisionReference = "KAJ/LCB/2026/0412";
        heights.SubdivisionApprovedOn = today.AddMonths(-8);
        var plot1 = await db.Units.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.AssetId == heights.Id && u.UnitNumber == "Plot 1");
        if (plot1 is not null) plot1.TitleNumber = "KAJIADO/KITENGELA/5678/1";

        db.ProjectCosts.AddRange(
            Cost(heights, CostKind.Budget, CostCategory.PurchaseCosts, "Stamp duty, legal and valuation", 450_000m),
            Cost(heights, CostKind.Budget, CostCategory.Subdivision, "Survey, mutation, beacons and titles", 550_000m),
            Cost(heights, CostKind.Budget, CostCategory.Infrastructure, "Roads, water and power", 3_000_000m),
            Cost(heights, CostKind.Budget, CostCategory.Approvals, "County approval and Land Control Board", 150_000m),
            Cost(heights, CostKind.Budget, CostCategory.Marketing, "Billboards, adverts and site days", 400_000m),
            Cost(heights, CostKind.Budget, CostCategory.Holding, "Security until sold out", 120_000m),

            Cost(heights, CostKind.Actual, CostCategory.PurchaseCosts, "Stamp duty (4%)", 320_000m, 11, "Kenya Revenue Authority"),
            Cost(heights, CostKind.Actual, CostCategory.PurchaseCosts, "Conveyancing fees", 120_000m, 11, "Mutua & Co. Advocates"),
            Cost(heights, CostKind.Actual, CostCategory.Approvals, "County approval and LCB consent", 150_000m, 9, "Kajiado County"),
            Cost(heights, CostKind.Actual, CostCategory.Subdivision, "Survey and mutation forms", 380_000m, 9, "Geo-Survey Kenya"),
            Cost(heights, CostKind.Actual, CostCategory.Subdivision, "Beacons", 60_000m, 8, "Geo-Survey Kenya"),
            Cost(heights, CostKind.Actual, CostCategory.Subdivision, "Title processing (12 titles)", 160_000m, 6, "Ministry of Lands"),
            Cost(heights, CostKind.Actual, CostCategory.Infrastructure, "Murram access roads", 1_750_000m, 7, "Kitengela Earthmovers"),
            Cost(heights, CostKind.Actual, CostCategory.Infrastructure, "Water line and meters", 650_000m, 5, "Olkejuado Water"),
            Cost(heights, CostKind.Actual, CostCategory.Marketing, "Billboards and online adverts", 180_000m, 4, "Ads Africa"),
            Cost(heights, CostKind.Actual, CostCategory.Holding, "Site security", 90_000m, 2, "Safeguard Security"));

        // ---- Acacia Court: flats built to let ----
        court.AcquisitionCost = 14_000_000m;
        court.EstimatedLandCost = 14_000_000m;
        court.CostAllocation = CostAllocationMethod.Equal;
        db.ProjectCosts.AddRange(
            Cost(court, CostKind.Budget, CostCategory.Construction, "Main contract: 2 blocks, 20 flats", 48_000_000m),
            Cost(court, CostKind.Budget, CostCategory.ProfessionalFees, "Architect, QS and engineers", 4_500_000m),
            Cost(court, CostKind.Budget, CostCategory.Approvals, "County building approval and NEMA", 1_000_000m),
            Cost(court, CostKind.Budget, CostCategory.Finance, "Construction loan interest", 3_000_000m),

            Cost(court, CostKind.Actual, CostCategory.Construction, "Main contract (final account)", 51_900_000m, 20, "Ruaka Builders Ltd"),
            Cost(court, CostKind.Actual, CostCategory.ProfessionalFees, "Architect, QS and engineers", 4_500_000m, 20, "Studio Mzima"),
            Cost(court, CostKind.Actual, CostCategory.Approvals, "County building approval and NEMA", 1_100_000m, 30, "Kiambu County"),
            Cost(court, CostKind.Actual, CostCategory.Finance, "Construction loan interest", 3_200_000m, 18, "KCB Bank"),
            Cost(court, CostKind.Actual, CostCategory.Letting, "Caretaker", 180_000m, 3, "Joseph Wekesa"),
            Cost(court, CostKind.Actual, CostCategory.Letting, "Repairs between tenants", 95_000m, 2, "Handy Fix"),
            Cost(court, CostKind.Actual, CostCategory.Letting, "Vacancy adverts", 25_000m, 1, "Online listings"));

        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 9: sales

    /// <summary>
    /// Savanna Homes sells plots at Savanna Heights (KES 1.45M each):
    /// Plot 1 completed cash sale; Plot 2 monthly instalments with the latest one overdue; Plot 3 a company paying
    /// quarterly (title transfer next); Plot 4 reserved with its deposit overdue; Plot 5 reserved, booking fee paid.
    /// </summary>
    private static async Task SeedSalesAsync(ApplicationDbContext db)
    {
        var savanna = await db.Businesses.FirstOrDefaultAsync(b => b.Name == "Savanna Homes Developers");
        if (savanna is null || await db.Buyers.IgnoreQueryFilters().AnyAsync(b => b.BusinessId == savanna.Id)) return;
        var id = savanna.Id;
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(3));

        // Keep the demo developer usable once its seeded paid period runs out.
        var paidThrough = await db.Subscriptions.Where(x => x.BusinessId == id).MaxAsync(x => (DateTime?)x.PeriodEndUtc);
        var proPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "PROFESSIONAL");
        if (proPlan is not null && (paidThrough is null || paidThrough.Value.AddDays(3) < now))
            db.Subscriptions.Add(new Subscription { BusinessId = id, PlanId = proPlan.Id, PeriodStartUtc = now, PeriodEndUtc = now.AddMonths(1), IsTrial = false });

        var plots = await db.Units.IgnoreQueryFilters()
            .Where(u => u.BusinessId == id && u.Asset.Name == "Savanna Heights" && !u.IsDeleted)
            .ToDictionaryAsync(u => u.UnitNumber);
        if (plots.Count < 5) return;

        long saleNo = 1, receiptNo = 1;
        var code = 700;
        string NextCode() { code++; return $"SV{code:D4}K{code % 10}T{(char)('A' + code % 26)}"; }

        Sale NewSale(string buyerName, OwnerType type, string phone, string plot, int daysAgo, PaymentPlan plan, decimal deposit, int count,
            InstalmentFrequency freq, int firstAfterMonths)
        {
            var buyer = new Buyer { BusinessId = id, FullName = buyerName, Type = type, Phone = phone, IdNumber = type == OwnerType.Individual ? $"2{code}4455" : null };
            var saleDate = today.AddDays(-daysAgo);
            DateOnly? first = plan == PaymentPlan.Instalments ? saleDate.AddMonths(firstAfterMonths) : null;
            var unit = plots[plot];
            var sale = new Sale
            {
                BusinessId = id, SaleNumber = $"SL-{saleNo++:00000}", Buyer = buyer, UnitId = unit.Id, SaleDate = saleDate, Price = unit.SalePrice ?? 1_450_000m,
                PaymentPlan = plan, DepositAmount = plan == PaymentPlan.FullPayment ? unit.SalePrice ?? 1_450_000m : deposit,
                InstalmentCount = plan == PaymentPlan.FullPayment ? 0 : count, InstalmentFrequency = freq, FirstInstalmentDate = first,
                Status = SaleStatus.Reserved
            };
            foreach (var l in SalesRules.BuildSchedule(sale.Price, sale.DepositAmount, plan, sale.InstalmentCount, freq, saleDate, first))
                sale.Instalments.Add(new SaleInstalment { BusinessId = id, Number = l.Number, Description = l.Description, DueDate = l.DueDate, Amount = l.Amount });
            foreach (var t in Enum.GetValues<MilestoneType>())
                sale.Milestones.Add(new SaleMilestone { BusinessId = id, Type = t });
            db.Sales.Add(sale);
            return sale;
        }

        void Pay(Sale sale, SaleInstalment instalment, decimal amount, DateOnly date)
        {
            var payment = new SalePayment
            {
                BusinessId = id, ReceiptNumber = $"SR-{receiptNo++:00000}", Sale = sale, PaymentDate = date <= today ? date : today,
                Amount = amount, Method = PaymentMethod.Mpesa, Reference = NextCode()
            };
            payment.Allocations.Add(new SaleAllocation { BusinessId = id, Instalment = instalment, Amount = amount });
            instalment.AmountPaid += amount;
            sale.AmountPaid += amount;
            db.SalePayments.Add(payment);
        }

        void Done(Sale sale, MilestoneType type, DateOnly on, string? reference = null)
        {
            var m = sale.Milestones.First(x => x.Type == type);
            m.CompletedOn = on <= today ? on : today;
            m.Reference = reference;
            if (type == MilestoneType.Agreement) sale.Status = SaleStatus.Active;
        }

        // Plot 1: cash, fully paid, every step done.
        var s1 = NewSale("Joseph Mwangi", OwnerType.Individual, "+254712900101", "Plot 1", 150, PaymentPlan.FullPayment, 0, 0, InstalmentFrequency.Monthly, 0);
        Pay(s1, s1.Instalments.First(), s1.Price, s1.SaleDate.AddDays(3));
        Done(s1, MilestoneType.Agreement, s1.SaleDate);
        Done(s1, MilestoneType.StampDuty, s1.SaleDate.AddDays(14), "KRA-SD-2026-1101");
        Done(s1, MilestoneType.TitleTransfer, s1.SaleDate.AddDays(60), "KAJIADO/KITENGELA/5678/1");
        Done(s1, MilestoneType.Handover, s1.SaleDate.AddDays(70));
        s1.Status = SaleStatus.Completed;
        s1.CompletedOn = s1.SaleDate.AddDays(70);

        // Plot 2: 20% deposit + 12 monthly; everything paid on time except the latest one due.
        var s2 = NewSale("Ruth Achieng", OwnerType.Individual, "+254712900202", "Plot 2", 200, PaymentPlan.Instalments, 290_000m, 12, InstalmentFrequency.Monthly, 1);
        var due2 = s2.Instalments.Where(i => i.DueDate < today).OrderBy(i => i.Number).ToList();
        foreach (var i in due2.Take(Math.Max(0, due2.Count - 1))) Pay(s2, i, i.Amount, i.DueDate.AddDays(2));
        Done(s2, MilestoneType.Agreement, s2.SaleDate);
        Done(s2, MilestoneType.StampDuty, s2.SaleDate.AddDays(20), "KRA-SD-2026-1188");

        // Plot 3: a company, 30% deposit + 6 quarterly; on track; title transfer is next.
        var s3 = NewSale("Kamau & Sons Ltd", OwnerType.Company, "+254720900303", "Plot 3", 100, PaymentPlan.Instalments, 435_000m, 6, InstalmentFrequency.Quarterly, 3);
        foreach (var i in s3.Instalments.Where(i => i.DueDate <= today)) Pay(s3, i, i.Amount, i.DueDate);
        Done(s3, MilestoneType.Agreement, s3.SaleDate);
        Done(s3, MilestoneType.StampDuty, s3.SaleDate.AddDays(21), "KRA-SD-2026-1240");

        // Plot 4: reserved 10 days ago; the 10% deposit is overdue.
        NewSale("Sarah Njeri", OwnerType.Individual, "+254712900404", "Plot 4", 10, PaymentPlan.Instalments, 145_000m, 24, InstalmentFrequency.Monthly, 1);

        // Plot 5: reserved 5 days ago; KES 50,000 booking fee paid towards the deposit.
        var s5 = NewSale("Daniel Kiptoo", OwnerType.Individual, "+254712900505", "Plot 5", 5, PaymentPlan.Instalments, 290_000m, 10, InstalmentFrequency.Monthly, 1);
        Pay(s5, s5.Instalments.First(i => i.Number == 0), 50_000m, s5.SaleDate);

        db.NumberSequences.AddRange(
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.Sale, NextValue = saleNo },
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.SaleReceipt, NextValue = receiptNo });
        await db.SaveChangesAsync();
    }

    /// <summary>James Kamau (agent) gets a login: a User member of Mji Property Agents linked to his agent profile.</summary>
    private static async Task LinkAgentLoginAsync(ApplicationDbContext db, IReadOnlyDictionary<string, string> userIds)
    {
        if (!userIds.TryGetValue(DemoAccounts.James, out var jamesUserId)) return;
        var mjiId = await db.Businesses.Where(b => b.Name == "Mji Property Agents").Select(b => b.Id).FirstOrDefaultAsync();
        if (mjiId == 0) return;

        var agent = await db.Agents.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.BusinessId == mjiId && a.FullName == "James Kamau" && !a.IsDeleted);
        if (agent is null || agent.UserId is not null) return;

        if (!await db.BusinessUsers.AnyAsync(m => m.BusinessId == mjiId && m.UserId == jamesUserId))
            db.BusinessUsers.Add(new BusinessUser { BusinessId = mjiId, UserId = jamesUserId, Role = BusinessRole.User });
        agent.UserId = jamesUserId;
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 8: agency

    /// <summary>
    /// Mji Property Agents manages two buildings for two landlords. Last month's rent is invoiced and mostly paid
    /// (W3 unpaid; A5 is a new lease whose deposit was paid and must be left out of the owner's remittance).
    /// Prepare the remittances yourself under Agency > Remittances.
    /// </summary>
    private static async Task SeedAgencyAsync(ApplicationDbContext db)
    {
        var mji = await db.Businesses.FirstOrDefaultAsync(b => b.Name == "Mji Property Agents");
        if (mji is null || await db.PropertyOwners.IgnoreQueryFilters().AnyAsync(o => o.BusinessId == mji.Id)) return;
        var id = mji.Id;
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(3));
        var lastMonth = today.AddMonths(-1);
        var (prevStart, prevEnd) = BillingRules.MonthBounds(lastMonth.Year, lastMonth.Month);

        // Keep the demo agency usable: give it a paid Agent plan month if its trial has run out.
        var paidThrough = await db.Subscriptions.Where(x => x.BusinessId == id).MaxAsync(x => (DateTime?)x.PeriodEndUtc);
        var agentPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "AGENT");
        if (agentPlan is not null && (paidThrough is null || paidThrough.Value.AddDays(3) < now))
            db.Subscriptions.Add(new Subscription { BusinessId = id, PlanId = agentPlan.Id, PeriodStartUtc = now, PeriodEndUtc = now.AddMonths(1), IsTrial = false });

        var grace = new PropertyOwner
        {
            BusinessId = id, FullName = "Grace Mwende", Type = OwnerType.Individual, IdNumber = "22345678", Phone = "+254711222333",
            Email = "grace.mwende@example.test", PayoutMethod = PayoutMethod.Bank, BankName = "Equity Bank", BankAccountName = "Grace Mwende", BankAccountNumber = "0123456789012"
        };
        var otieno = new PropertyOwner
        {
            BusinessId = id, FullName = "Otieno Family Trust", Type = OwnerType.Company, KraPin = "P051112223Q", Phone = "+254722333444",
            Email = "trust@otieno.example.test", PayoutMethod = PayoutMethod.Mpesa, MpesaNumber = "+254722333444"
        };
        var james = new Agent { BusinessId = id, FullName = "James Kamau", LicenseNumber = "EARB/1234/2024", Phone = "+254733100200", CommissionSharePercent = 40m };
        var wanjiru = new Agent { BusinessId = id, FullName = "Wanjiru Njoroge", LicenseNumber = "EARB/2210/2023", Phone = "+254733100300", CommissionSharePercent = 35m };

        Asset Building(string name, string location, string prefix, int count, decimal rent)
        {
            var a = new Asset { BusinessId = id, Name = name, Type = AssetType.Building, Location = location, County = "Nairobi", LifecycleState = AssetLifecycle.Operational };
            for (var i = 1; i <= count; i++)
                a.Units.Add(new Unit { BusinessId = id, UnitNumber = $"{prefix}{i}", Type = UnitType.Apartment, Bedrooms = 2, Bathrooms = 2, MonthlyRent = rent, Status = UnitStatus.Available });
            return a;
        }
        var kilimani = Building("Kilimani Heights", "Argwings Kodhek Road, Kilimani", "A", 6, 45_000m);
        var westlands = Building("Westlands Court", "Waiyaki Way, Westlands", "W", 4, 60_000m);

        db.AddRange(grace, otieno, james, wanjiru, kilimani, westlands);
        await db.SaveChangesAsync();

        long leaseNo = 1, invoiceNo = 1, receiptNo = 1;
        string[] names = ["Peter Mutiso", "Agnes Wambui", "Collins Barasa", "Diana Chepkoech", "Eric Omondi", "Fatuma Ali", "George Kiprono", "Hellen Akoth"];
        var tenantIdx = 0;
        var code = 500;

        void Let(Asset asset, string unitNo, DateOnly start, bool pays)
        {
            var unit = asset.Units.First(u => u.UnitNumber == unitNo);
            unit.Status = UnitStatus.Occupied;
            var tenant = new Tenant { BusinessId = id, FullName = names[tenantIdx], Phone = $"+2547441000{tenantIdx:00}", IdNumber = $"3{tenantIdx}112233" };
            tenantIdx++;
            var lease = new Lease
            {
                BusinessId = id, LeaseNumber = $"L-{leaseNo++:00000}", Tenant = tenant, UnitId = unit.Id, Status = LeaseStatus.Active,
                StartDate = start, EndDate = start.AddYears(1).AddDays(-1), MonthlyRent = unit.MonthlyRent, DepositAmount = unit.MonthlyRent,
                RentDueDay = 5, ActivatedAtUtc = now
            };
            var invoice = new Invoice
            {
                BusinessId = id, InvoiceNumber = InvoiceService.Format(invoiceNo++), Kind = InvoiceKind.Monthly, Tenant = tenant, Lease = lease,
                InvoiceDate = prevStart, DueDate = BillingRules.DueDate(prevStart, 5, start), PeriodStart = prevStart, PeriodEnd = prevEnd,
                Status = InvoiceStatus.Pending, IssuedAtUtc = prevStart.ToDateTime(TimeOnly.MinValue)
            };
            invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Rent, $"Rent, {prevStart:MMMM yyyy}", 1, unit.MonthlyRent, 0));
            if (start >= prevStart)   // brand-new lease: the deposit is on its first invoice
                invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Deposit, "Security deposit (refundable)", 1, lease.DepositAmount, 0));
            foreach (var l in invoice.Lines) l.BusinessId = id;
            InvoiceComposer.Recalculate(invoice);
            db.Invoices.Add(invoice);

            if (!pays) return;
            var payment = new Payment
            {
                BusinessId = id, ReceiptNumber = $"RCT-{receiptNo++:00000}", Tenant = tenant, Lease = lease,
                PaymentDate = invoice.DueDate.AddDays(1) <= today ? invoice.DueDate.AddDays(1) : today,
                Amount = invoice.Total, Method = PaymentMethod.Mpesa, Reference = $"SM{++code:D4}Q{code % 10}P{(char)('A' + code % 26)}"
            };
            payment.Allocations.Add(new PaymentAllocation { BusinessId = id, Invoice = invoice, Amount = invoice.Total });
            invoice.AmountPaid = invoice.Total;
            db.Payments.Add(payment);
        }

        var longAgo = prevStart.AddMonths(-6);
        foreach (var u in new[] { "A1", "A2", "A3", "A4" }) Let(kilimani, u, longAgo, pays: true);
        Let(kilimani, "A5", prevStart, pays: true);   // new lease: deposit paid, excluded from the remittance
        Let(westlands, "W1", longAgo, pays: true);
        Let(westlands, "W2", longAgo, pays: true);
        Let(westlands, "W3", longAgo, pays: false);  // unpaid: nothing to remit for it

        var c1 = new ManagementContract
        {
            BusinessId = id, ContractNumber = "MC-00001", Owner = grace, Agent = james, StartDate = longAgo, Status = ContractStatus.Active,
            CommissionType = CommissionType.PercentOfCollections, CommissionPercent = 10m, RemittanceDay = 10
        };
        var c2 = new ManagementContract
        {
            BusinessId = id, ContractNumber = "MC-00002", Owner = otieno, Agent = wanjiru, StartDate = longAgo, Status = ContractStatus.Active,
            CommissionType = CommissionType.PercentOfCollections, CommissionPercent = 8m, RemittanceDay = 10
        };
        c1.Properties.Add(new ContractProperty { BusinessId = id, AssetId = kilimani.Id, AddedOn = longAgo, IsActive = true });
        c2.Properties.Add(new ContractProperty { BusinessId = id, AssetId = westlands.Id, AddedOn = longAgo, IsActive = true });
        db.ManagementContracts.AddRange(c1, c2);

        db.NumberSequences.AddRange(
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.Lease, NextValue = leaseNo },
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.Invoice, NextValue = invoiceNo },
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.Receipt, NextValue = receiptNo },
            new NumberSequence { BusinessId = id, Key = Core.Interfaces.SequenceKeys.Contract, NextValue = 3 });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 6: credits and collections

    /// <summary>
    /// Demo businesses were created directly (not through sign-up), so give each its plan's credits once.
    /// Acacia Gardens: Lucy is "in arrears" with a promise to pay; Moses (bounced cheque) has been served a demand.
    /// </summary>
    private static async Task SeedArrearsAsync(ApplicationDbContext db)
    {
        var demoNames = new[] { MarkerBusiness, "Savanna Homes Developers", "Mji Property Agents", "Lapsed Holdings" };
        var businesses = await db.Businesses.Where(b => demoNames.Contains(b.Name)).ToListAsync();
        foreach (var business in businesses)
        {
            if (await db.CreditTransactions.AnyAsync(t => t.BusinessId == business.Id)) continue;
            db.CreditTransactions.Add(CreditLedger.Post(business, 200, CreditTransactionType.Allocation, "Professional plan credits (demo)"));
        }
        await db.SaveChangesAsync();

        var acaciaId = businesses.FirstOrDefault(b => b.Name == MarkerBusiness)?.Id ?? 0;
        if (acaciaId == 0 || await db.CollectionNotes.IgnoreQueryFilters().AnyAsync(n => n.BusinessId == acaciaId)) return;

        var tenants = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.BusinessId == acaciaId && (t.FullName == "Lucy Njeri" || t.FullName == "Moses Odhiambo"))
            .ToDictionaryAsync(t => t.FullName);
        if (tenants.Count < 2) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var lucy = tenants["Lucy Njeri"];
        var moses = tenants["Moses Odhiambo"];
        lucy.CollectionStage = CollectionStage.InArrears;
        moses.CollectionStage = CollectionStage.DemandServed;

        CollectionNote Note(Tenant t, CollectionStage stage, bool changed, string text, DateOnly? promise = null)
            => new() { BusinessId = acaciaId, TenantId = t.Id, Stage = stage, StageChanged = changed, Note = text, PromisedPaymentDate = promise };

        db.CollectionNotes.AddRange(
            Note(lucy, CollectionStage.InArrears, true, "Paid 10,000 of last month's rent. Called her: salary delayed."),
            Note(lucy, CollectionStage.InArrears, false, "Promised to clear the balance by M-PESA.", today.AddDays(5)),
            Note(moses, CollectionStage.InArrears, true, "Cheque 000451 returned unpaid. Informed tenant by phone."),
            Note(moses, CollectionStage.DemandServed, true, "Formal demand letter delivered by hand; 14 days to pay."));
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 5: collections

    /// <summary>
    /// Payments against last month's invoices: most paid in full; Lucy paid part; Mercy overpaid (credit);
    /// David and Kwa Njeri Salon haven't paid; Moses's cheque bounced and was reversed.
    /// Hassan's deposit (18,000 less 5,000 deductions) has been refunded.
    /// </summary>
    private static async Task SeedCollectionsAsync(ApplicationDbContext db)
    {
        var acaciaId = await db.Businesses.Where(b => b.Name == MarkerBusiness).Select(b => b.Id).FirstOrDefaultAsync();
        if (acaciaId == 0) return;
        if (await db.Payments.IgnoreQueryFilters().AnyAsync(p => p.BusinessId == acaciaId)) return;

        var invoices = await db.Invoices.IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .Where(i => i.BusinessId == acaciaId && !i.IsDeleted && i.Status == InvoiceStatus.Pending)
            .OrderBy(i => i.Id)
            .ToListAsync();
        if (invoices.Count == 0) return;

        var adminId = await db.Users.Where(u => u.Email == DemoAccounts.Admin).Select(u => u.Id).FirstOrDefaultAsync();
        var receipt = 1L;
        var code = 0;
        string MpesaCode() => $"SK{++code:D4}Q{code % 10}P{(char)('A' + code % 26)}";   // SK0001Q1PB: 10 characters

        Payment Pay(Invoice inv, decimal amount, PaymentMethod method, string? reference, DateOnly date, decimal allocate)
        {
            var payment = new Payment
            {
                BusinessId = acaciaId, ReceiptNumber = $"RCT-{receipt++:00000}", TenantId = inv.TenantId, LeaseId = inv.LeaseId,
                PaymentDate = date, Amount = amount, Method = method, Reference = reference, PayerName = inv.Tenant.FullName
            };
            if (allocate > 0)
            {
                payment.Allocations.Add(new PaymentAllocation { BusinessId = acaciaId, Invoice = inv, Amount = allocate });
                inv.AmountPaid += allocate;
            }
            db.Payments.Add(payment);
            return payment;
        }

        foreach (var inv in invoices)
        {
            var name = inv.Tenant.FullName;
            var paidOn = inv.DueDate.AddDays(inv.Id % 4);
            switch (name)
            {
                case "David Ochieng":
                case "Kwa Njeri Salon":
                    break;   // not paid: overdue
                case "Lucy Njeri":
                    Pay(inv, 10_000m, PaymentMethod.Mpesa, MpesaCode(), paidOn, 10_000m);
                    break;
                case "Mercy Achieng":
                    Pay(inv, inv.Total + 2_000m, PaymentMethod.Mpesa, MpesaCode(), paidOn, inv.Total);   // 2,000 credit
                    break;
                case "Moses Odhiambo":
                    var bounced = Pay(inv, inv.Total, PaymentMethod.Cheque, "000451", paidOn, inv.Total);
                    bounced.Status = PaymentStatus.Reversed;
                    bounced.ReversedAtUtc = paidOn.AddDays(5).ToDateTime(new TimeOnly(9, 0));
                    bounced.ReversedBy = adminId;
                    bounced.ReversalReason = "Cheque returned unpaid (insufficient funds)";
                    foreach (var a in bounced.Allocations) a.IsReversed = true;
                    inv.AmountPaid -= inv.Total;
                    break;
                default:
                    var isCompany = inv.Tenant.Type == TenantType.Company;
                    Pay(inv, inv.Total, isCompany ? PaymentMethod.BankTransfer : PaymentMethod.Mpesa,
                        isCompany ? $"FT{inv.Id:D8}" : MpesaCode(), paidOn, inv.Total);
                    break;
            }
        }

        // Hassan's terminated lease: deposit 18,000 less 5,000 deductions refunded.
        var hassanLease = await db.Leases.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.BusinessId == acaciaId && l.Status == LeaseStatus.Terminated && l.Tenant.FullName == "Hassan Omar");
        var refundNo = 1L;
        if (hassanLease is not null)
            db.Refunds.Add(new Refund
            {
                BusinessId = acaciaId, RefundNumber = $"RFD-{refundNo++:00000}", Kind = RefundKind.Deposit,
                TenantId = hassanLease.TenantId, LeaseId = hassanLease.Id, RefundDate = hassanLease.EndedOn!.Value.AddDays(7),
                Amount = 13_000m, Method = PaymentMethod.Mpesa, Reference = "SKREFUND01", Notes = "Deposit less repainting and window"
            });

        db.NumberSequences.AddRange(
            new NumberSequence { BusinessId = acaciaId, Key = Core.Interfaces.SequenceKeys.Receipt, NextValue = receipt },
            new NumberSequence { BusinessId = acaciaId, Key = Core.Interfaces.SequenceKeys.Refund, NextValue = refundNo });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 4: billing

    /// <summary>
    /// Acacia Gardens: charge library, charges on every active lease, water meters (opening reading + last
    /// month's reading) and LAST MONTH's invoices, issued and now overdue (no payments until Sprint 5).
    /// This month is left for you to bill: enter readings on the meter sheet, then run Generate invoices.
    /// </summary>
    private static async Task SeedBillingAsync(ApplicationDbContext db)
    {
        var acaciaId = await db.Businesses.Where(b => b.Name == MarkerBusiness).Select(b => b.Id).FirstOrDefaultAsync();
        if (acaciaId == 0) return;
        if (await db.ChargeDefinitions.IgnoreQueryFilters().AnyAsync(c => c.BusinessId == acaciaId)) return;

        var leases = await db.Leases.IgnoreQueryFilters()
            .Include(l => l.Unit)
            .Where(l => l.BusinessId == acaciaId && !l.IsDeleted && l.Status == LeaseStatus.Active)
            .ToListAsync();
        if (leases.Count == 0) return;

        ChargeDefinition Def(string name, ChargeType type, decimal? amount, decimal? rate, string? uom, decimal tax, string? description = null)
            => new() { BusinessId = acaciaId, Name = name, Type = type, DefaultAmount = amount, UnitRate = rate, UnitOfMeasure = uom, TaxPercent = tax, Description = description };

        var garbage = Def("Garbage collection", ChargeType.Recurring, 300m, null, null, 0m);
        var security = Def("Security", ChargeType.Recurring, 500m, null, null, 0m);
        var water = Def("Water", ChargeType.Utility, null, 150m, "m³", 0m, "Nairobi Water bulk rate passed through");
        var service = Def("Service charge", ChargeType.Recurring, 2_000m, null, null, 16m, "Commercial units, VAT inclusive of 16%");
        var keys = Def("Key replacement", ChargeType.OneTime, 1_500m, null, null, 0m);
        var late = Def("Late payment penalty", ChargeType.Penalty, 1_000m, null, null, 0m);
        db.ChargeDefinitions.AddRange(garbage, security, water, service, keys, late);

        foreach (var lease in leases)
        {
            var isShop = lease.Unit.Type == UnitType.Shop;
            LeaseCharge Attach(ChargeDefinition d, decimal? amount = null)
                => new() { BusinessId = acaciaId, LeaseId = lease.Id, ChargeDefinition = d, AmountOverride = amount };

            db.LeaseCharges.Add(Attach(garbage));
            if (isShop) db.LeaseCharges.Add(Attach(service));
            else
            {
                db.LeaseCharges.Add(Attach(security));
                db.LeaseCharges.Add(Attach(water));
            }
        }
        await db.SaveChangesAsync();

        // Water meters on occupied flats: an opening reading and last month's reading.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var lastMonth = today.AddMonths(-1);
        var (prevStart, prevEnd) = BillingRules.MonthBounds(lastMonth.Year, lastMonth.Month);
        var readingsByUnit = new Dictionary<int, UtilityMeterReading>();
        var i = 0;
        foreach (var lease in leases.Where(l => l.Unit.Type != UnitType.Shop).OrderBy(l => l.Unit.UnitNumber))
        {
            var opening = 100m + i * 13;
            var used = 8m + i % 7;
            i++;
            db.UtilityMeterReadings.Add(new UtilityMeterReading
            {
                BusinessId = acaciaId, UnitId = lease.UnitId, ChargeDefinition = water, ReadingDate = prevStart.AddDays(-1),
                PreviousReading = opening, CurrentReading = opening, Consumption = 0, UnitRate = 150m, Amount = 0, IsOpening = true
            });
            var reading = new UtilityMeterReading
            {
                BusinessId = acaciaId, UnitId = lease.UnitId, ChargeDefinition = water, ReadingDate = prevEnd,
                PreviousReading = opening, CurrentReading = opening + used, Consumption = used, UnitRate = 150m, Amount = used * 150m
            };
            db.UtilityMeterReadings.Add(reading);
            readingsByUnit[lease.UnitId] = reading;
        }
        await db.SaveChangesAsync();

        // Last month's invoices, issued on the 1st and now overdue.
        var next = 1L;
        var monthName = prevStart.ToString("MMMM yyyy");
        foreach (var lease in leases.Where(l => l.StartDate <= prevEnd).OrderBy(l => l.Unit.UnitNumber, NaturalComparer.Instance))
        {
            var invoice = new Invoice
            {
                BusinessId = acaciaId, InvoiceNumber = InvoiceService.Format(next++), Kind = InvoiceKind.Monthly,
                TenantId = lease.TenantId, LeaseId = lease.Id, InvoiceDate = prevStart,
                DueDate = BillingRules.DueDate(prevStart, lease.RentDueDay, lease.StartDate),
                PeriodStart = prevStart, PeriodEnd = prevEnd, Status = InvoiceStatus.Pending,
                IssuedAtUtc = prevStart.ToDateTime(TimeOnly.MinValue)
            };

            var (rent, days, daysInPeriod) = BillingRules.RentForPeriod(lease.MonthlyRent, lease.StartDate, prevStart, prevEnd);
            invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Rent,
                days == daysInPeriod ? $"Rent, {monthName}" : $"Rent, {monthName} ({days} of {daysInPeriod} days)", 1, rent, 0));

            var isShop = lease.Unit.Type == UnitType.Shop;
            invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.RecurringCharge, $"Garbage collection, {monthName}", 1, 300m, 0, garbage.Id));
            if (isShop)
                invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.RecurringCharge, $"Service charge, {monthName}", 1, 2_000m, 16m, service.Id));
            else
                invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.RecurringCharge, $"Security, {monthName}", 1, 500m, 0, security.Id));

            if (readingsByUnit.TryGetValue(lease.UnitId, out var r))
            {
                invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Utility,
                    $"Water: {r.PreviousReading:0.###} to {r.CurrentReading:0.###} m³ (read {r.ReadingDate:d MMM})", r.Consumption, r.UnitRate, 0, water.Id, r.Id));
                r.BilledInvoice = invoice;
            }

            foreach (var line in invoice.Lines) line.BusinessId = acaciaId;
            InvoiceComposer.Recalculate(invoice);
            db.Invoices.Add(invoice);
        }

        db.NumberSequences.Add(new NumberSequence { BusinessId = acaciaId, Key = Core.Interfaces.SequenceKeys.Invoice, NextValue = next });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 3: tenants and leases

    /// <summary>
    /// Acacia Gardens gets: 15 active leases (one ending in 20 days, one in 50, one already past its end date),
    /// a renewal chain, a terminated lease with a deposit deduction, a draft lease and a prospective tenant.
    /// Units with active leases are set to Occupied in the same save, exactly as LeaseService does.
    /// </summary>
    private static async Task SeedTenanciesAsync(ApplicationDbContext db)
    {
        var acaciaId = await db.Businesses.Where(b => b.Name == MarkerBusiness).Select(b => b.Id).FirstOrDefaultAsync();
        if (acaciaId == 0) return;
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.BusinessId == acaciaId)) return;

        var units = await db.Units.IgnoreQueryFilters()
            .Where(u => u.BusinessId == acaciaId && !u.IsDeleted)
            .ToDictionaryAsync(u => u.UnitNumber);
        if (!units.ContainsKey("A002") || !units.ContainsKey("S1")) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var next = 1L;
        string Number() => $"L-{next++:00000}";

        Tenant Person(string name, string id, string phone, string employer) => new()
        {
            BusinessId = acaciaId, Type = TenantType.Individual, FullName = name, IdType = IdDocumentType.NationalId,
            IdNumber = id, Phone = phone, Employer = employer, Email = $"{name.Split(' ')[0].ToLowerInvariant()}@example.test",
            EmergencyContactName = "Next of kin", EmergencyContactPhone = "+254700000000", EmergencyContactRelationship = "Relative"
        };
        Tenant Company(string name, string reg, string phone) => new()
        {
            BusinessId = acaciaId, Type = TenantType.Company, FullName = name, IdType = IdDocumentType.CompanyRegistration,
            IdNumber = reg, KraPin = $"P05{reg[^7..]}Z", Phone = phone
        };
        Lease ActiveLease(Tenant t, string unitNo, DateOnly start, DateOnly? end, int dueDay = 1)
        {
            var unit = units[unitNo];
            unit.Status = UnitStatus.Occupied;
            return new Lease
            {
                BusinessId = acaciaId, LeaseNumber = Number(), Tenant = t, UnitId = unit.Id, Status = LeaseStatus.Active,
                StartDate = start, EndDate = end, MonthlyRent = unit.MonthlyRent, DepositAmount = unit.MonthlyRent,
                NoticePeriodDays = 30, RentDueDay = dueDay, ActivatedAtUtc = DateTime.UtcNow
            };
        }

        var mercy = Person("Mercy Achieng", "28451903", "+254712000101", "Safaricom");
        var john = Person("John Mwangi", "30128876", "+254712000102", "KCB Bank");
        var esther = Person("Esther Wanjiru", "27765410", "+254712000103", "Ruaka Primary School");
        var david = Person("David Ochieng", "31452299", "+254712000104", "Self-employed");
        var lucy = Person("Lucy Njeri", "29981234", "+254712000105", "Kenya Power");
        var samuel = Person("Samuel Kiptoo", "32210987", "+254712000106", "Equity Bank");
        var joyce = Person("Joyce Atieno", "26654321", "+254712000107", "Nairobi Hospital");
        var daniel = Person("Daniel Kariuki", "33012456", "+254712000108", "Twiga Foods");
        var ann = Person("Ann Wairimu", "25543210", "+254712000109", "Ministry of Health");
        var moses = Person("Moses Odhiambo", "34109876", "+254712000110", "Uber driver");
        var caroline = Person("Caroline Chebet", "28890011", "+254712000111", "KenGen");
        var patrick = Person("Patrick Mutua", "30776655", "+254712000112", "Bolt");
        var naomi = Person("Naomi Akinyi", "35501234", "+254712000113", "Strathmore University");
        var hassan = Person("Hassan Omar", "24412300", "+254712000114", "Moved to Mombasa");
        var brian = Person("Brian Kimani", "36603344", "+254712000115", "Andela");   // prospective: no lease
        var shop1 = Company("Mama Mboga Fresh Ltd", "PVT-2019-0044512", "+254722000201");
        var shop2 = Company("Ruaka Pharmacy Ltd", "PVT-2016-0031177", "+254722000202");
        var shop3 = Company("Kwa Njeri Salon", "BN-2021-0098341", "+254722000203");

        db.Tenants.AddRange(mercy, john, esther, david, lucy, samuel, joyce, daniel, ann, moses, caroline, patrick,
            naomi, hassan, brian, shop1, shop2, shop3);

        // John: last year's lease was renewed into this year's.
        var johnOld = new Lease
        {
            BusinessId = acaciaId, LeaseNumber = Number(), Tenant = john, UnitId = units["A003"].Id, Status = LeaseStatus.Renewed,
            StartDate = today.AddMonths(-16), EndDate = today.AddMonths(-4).AddDays(-1), EndedOn = today.AddMonths(-4).AddDays(-1),
            MonthlyRent = 26_000m, DepositAmount = 26_000m, NoticePeriodDays = 30, RentDueDay = 1, ActivatedAtUtc = DateTime.UtcNow
        };
        // Hassan: terminated two months ago with KES 5,000 deducted from his deposit for repairs.
        var hassanLease = new Lease
        {
            BusinessId = acaciaId, LeaseNumber = Number(), Tenant = hassan, UnitId = units["A004"].Id, Status = LeaseStatus.Terminated,
            StartDate = today.AddMonths(-14), EndDate = today.AddMonths(-2), EndedOn = today.AddMonths(-2),
            NoticeGivenOn = today.AddMonths(-2).AddDays(-30), TerminationReason = "Relocated for work",
            TerminationPenalty = 0m, DepositDeductions = 5_000m, DepositNote = "Repainting and broken window",
            MonthlyRent = 18_000m, DepositAmount = 18_000m, NoticePeriodDays = 30, RentDueDay = 1, ActivatedAtUtc = DateTime.UtcNow
        };
        db.Leases.AddRange(johnOld, hassanLease);

        var johnNew = ActiveLease(john, "A003", today.AddMonths(-4), today.AddMonths(8));
        johnNew.RenewedFrom = johnOld;
        johnNew.MonthlyRent = 28_000m;

        db.Leases.AddRange(
            ActiveLease(mercy, "A002", today.AddMonths(-7), today.AddMonths(5)),
            johnNew,
            ActiveLease(esther, "A101", today.AddMonths(-12).AddDays(20), today.AddDays(20)),     // ends in 20 days
            ActiveLease(david, "A102", today.AddMonths(-10), today.AddDays(50)),                  // ends in 50 days
            ActiveLease(lucy, "A103", today.AddMonths(-12).AddDays(-5), today.AddDays(-5)),       // ended 5 days ago, still active
            ActiveLease(samuel, "A104", today.AddMonths(-3), today.AddMonths(9), dueDay: 5),
            ActiveLease(joyce, "A201", today.AddMonths(-6), today.AddMonths(6)),
            ActiveLease(daniel, "A202", today.AddMonths(-2), today.AddMonths(10)),
            ActiveLease(ann, "B001", today.AddMonths(-20), null),                                  // open-ended
            ActiveLease(moses, "B002", today.AddMonths(-5), today.AddMonths(7)),
            ActiveLease(caroline, "B101", today.AddMonths(-8), today.AddMonths(4)),
            ActiveLease(patrick, "B102", today.AddMonths(-1), today.AddMonths(11)),
            ActiveLease(shop1, "S1", today.AddMonths(-24), today.AddMonths(12), dueDay: 5),
            ActiveLease(shop2, "S2", today.AddMonths(-30), today.AddMonths(30), dueDay: 5),
            ActiveLease(shop3, "S3", today.AddMonths(-4), today.AddMonths(20), dueDay: 5));

        // Naomi: a draft lease being prepared for B104.
        db.Leases.Add(new Lease
        {
            BusinessId = acaciaId, LeaseNumber = Number(), Tenant = naomi, UnitId = units["B104"].Id, Status = LeaseStatus.Draft,
            StartDate = today.AddDays(10), EndDate = today.AddDays(10).AddYears(1), MonthlyRent = 35_000m,
            DepositAmount = 70_000m, NoticePeriodDays = 60, RentDueDay = 1, Notes = "Two months' deposit agreed"
        });

        db.NumberSequences.Add(new NumberSequence { BusinessId = acaciaId, Key = Core.Interfaces.SequenceKeys.Lease, NextValue = next });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Sprint 2: portfolio

    private const string MarkerAsset = "Acacia Court";

    /// <summary>
    /// Units are seeded Available/Reserved/Inactive/Sold only. Occupied comes from leases (Sprint 3),
    /// so demo data never contains an occupied unit without a lease.
    /// Seeder runs with no business selected, so BusinessId is set explicitly on every tenant row.
    /// </summary>
    private static async Task SeedPortfolioAsync(ApplicationDbContext db)
    {
        if (await db.Assets.IgnoreQueryFilters().AnyAsync(a => a.Name == MarkerAsset))
            return;

        var businesses = await db.Businesses
            .Where(b => b.Name == MarkerBusiness || b.Name == "Savanna Homes Developers" || b.Name == "Lapsed Holdings")
            .ToDictionaryAsync(b => b.Name, b => b.Id);
        if (!businesses.ContainsKey(MarkerBusiness) || !businesses.ContainsKey("Savanna Homes Developers")) return;

        // Acacia Gardens: an estate with two blocks and floors (20 flats) + a row of shops on the road.
        var acaciaId = businesses[MarkerBusiness];
        var court = NewAsset(acaciaId, MarkerAsset, AssetType.Estate, "Ruaka, off Limuru Road", "Kiambu", 1.5m, 48_000_000m, "KIAMBU/RUAKA/1234");
        var blockA = Node(court, null, "Block A", StructureNodeType.Block, 1);
        var blockB = Node(court, null, "Block B", StructureNodeType.Block, 2);
        string[] floorNames = ["Ground floor", "First floor", "Second floor"];
        for (var f = 0; f < 3; f++)
        {
            var floor = Node(court, blockA, floorNames[f], StructureNodeType.Floor, f);
            for (var u = 1; u <= 4; u++)
                Flat(court, floor, $"A{f}{u:00}", f, bedrooms: u <= 2 ? 2 : 1, rent: u <= 2 ? 28_000m : 18_000m,
                    status: (f, u) switch { (0, 1) => UnitStatus.Reserved, (2, 4) => UnitStatus.Inactive, _ => UnitStatus.Available });
        }
        for (var f = 0; f < 2; f++)
        {
            var floor = Node(court, blockB, floorNames[f], StructureNodeType.Floor, f);
            for (var u = 1; u <= 4; u++)
                Flat(court, floor, $"B{f}{u:00}", f, bedrooms: 3, rent: 35_000m, status: UnitStatus.Available);
        }

        var shops = NewAsset(acaciaId, "Acacia Shopping Centre", AssetType.CommercialComplex, "Ruaka town", "Kiambu", 0.25m, 15_000_000m, null);
        for (var i = 1; i <= 6; i++)
            shops.Units.Add(new Unit
            {
                BusinessId = acaciaId, UnitNumber = $"S{i}", Type = UnitType.Shop, SizeSqm = 24m,
                MonthlyRent = 22_000m, Status = i == 6 ? UnitStatus.Reserved : UnitStatus.Available
            });

        // Savanna Homes (developer): plots for sale.
        var savannaId = businesses["Savanna Homes Developers"];
        var heights = NewAsset(savannaId, "Savanna Heights", AssetType.Land, "Kitengela", "Kajiado", 5m, 30_000_000m, "KAJIADO/KITENGELA/5678");
        heights.LifecycleState = AssetLifecycle.ForSale;
        for (var i = 1; i <= 12; i++)
            heights.Units.Add(new Unit
            {
                BusinessId = savannaId, UnitNumber = $"Plot {i}", Type = UnitType.Plot, SizeSqm = 450m,
                SalePrice = 1_450_000m, MonthlyRent = 0m,
                Status = i <= 3 ? UnitStatus.Sold : i <= 5 ? UnitStatus.Reserved : UnitStatus.Available
            });

        db.Assets.AddRange(court, shops, heights);

        // Lapsed Holdings (development seed only): 9 units. Its pending payment is for Starter (10 units), so after
        // approval you can add exactly one more unit before hitting the plan limit.
        if (businesses.TryGetValue("Lapsed Holdings", out var lapsedId))
        {
            var flats = NewAsset(lapsedId, "Kiprop Flats", AssetType.Building, "Kericho town", "Kericho", 0.125m, null, null);
            for (var i = 1; i <= 9; i++)
                flats.Units.Add(new Unit
                {
                    BusinessId = lapsedId, UnitNumber = $"{i}", Type = UnitType.Bedsitter, MonthlyRent = 7_500m,
                    FloorNumber = (i - 1) / 3, Status = UnitStatus.Available
                });
            db.Assets.Add(flats);
        }
        await db.SaveChangesAsync();
    }

    private static Asset NewAsset(int businessId, string name, AssetType type, string location, string county,
        decimal? acres, decimal? cost, string? titleDeed)
        => new()
        {
            BusinessId = businessId, Name = name, Type = type, Location = location, County = county,
            LandSizeAcres = acres, AcquisitionCost = cost, TitleDeedNumber = titleDeed,
            LifecycleState = AssetLifecycle.Operational
        };

    private static StructureNode Node(Asset asset, StructureNode? parent, string name, StructureNodeType type, int sort)
    {
        var node = new StructureNode
        {
            BusinessId = asset.BusinessId, Asset = asset, Parent = parent, Name = name, NodeType = type, SortOrder = sort
        };
        asset.Nodes.Add(node);
        return node;
    }

    private static void Flat(Asset asset, StructureNode floor, string number, int floorNumber, int bedrooms, decimal rent, UnitStatus status)
        => asset.Units.Add(new Unit
        {
            BusinessId = asset.BusinessId, StructureNode = floor, UnitNumber = number, FloorNumber = floorNumber,
            Type = UnitType.Apartment, Bedrooms = bedrooms, Bathrooms = bedrooms > 1 ? 2 : 1,
            SizeSqm = bedrooms * 35m + 20m, MonthlyRent = rent, Status = status
        });

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> users, DemoAccounts.Account account, string password)
    {
        var user = await users.FindByEmailAsync(account.Email);
        if (user is not null) return user;

        user = new ApplicationUser { UserName = account.Email, Email = account.Email, EmailConfirmed = true, FullName = account.FullName };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Demo user {account.Email} failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static Business NewBusiness(string name, BusinessType type, string? kraPin, params (string UserId, BusinessRole Role)[] members)
    {
        var business = new Business
        {
            Name = name, Type = type, KraPin = kraPin,
            Email = $"info@{name.Split(' ')[0].ToLowerInvariant()}.test", Phone = "0712 345 678", PhysicalAddress = "Nairobi"
        };
        foreach (var (userId, role) in members)
            business.Members.Add(new BusinessUser { UserId = userId, Role = role });
        return business;
    }

    private static void AddTrial(ApplicationDbContext db, Business business, SubscriptionPlan plan, DateTime start)
        => db.Subscriptions.Add(new Subscription
        {
            Business = business, PlanId = plan.Id, PeriodStartUtc = start,
            PeriodEndUtc = start.AddDays(plan.TrialDays), IsTrial = true
        });
}
