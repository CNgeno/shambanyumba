using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Filters;
using RealEstateERP.Web.Models;

namespace RealEstateERP.Web.Controllers;

/// <summary>The signed-in app home. Requires a selected business and an active subscription.</summary>
public class DashboardController(
    ISubscriptionService subscriptions,
    IAssetService assets,
    ILeaseService leases,
    IInvoiceService invoices,
    IPaymentService payments,
    IAgencyService agency,
    IRemittanceService remittances,
    IAgentScopeService agentScope,
    ISaleService sales,
    IProjectCostService costing,
    IEstateService estate,
    ICapabilityService capabilities,
    IReportService reports,
    IListingService listings,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var snapshot = HttpContext.Items[SubscriptionGateFilter.SnapshotItemKey] as SubscriptionSnapshot
            ?? await subscriptions.GetSnapshotAsync(currentUser.BusinessId!.Value, ct);
        var totals = await assets.GetTotalsAsync(ct);
        var hasUnits = totals.Stats.Total > 0;
        var counts = await leases.GetCountsAsync(ct);
        var hasLeases = counts.Active > 0;
        var billing = await invoices.GetSummaryAsync(ct);

        AgencyPanel? agencyPanel = null;
        if (currentUser.BusinessType == BusinessType.Agency)
        {
            var scope = await agentScope.GetAsync(ct);
            var lastMonth = RealEstateERP.Core.Common.KenyaTime.TodayEat(clock).AddMonths(-1);
            lastMonth = new DateOnly(lastMonth.Year, lastMonth.Month, 1);
            var contracts = await agency.ListContractsAsync(null, ContractStatus.Active, ct);
            var statements = (await remittances.ListAsync(lastMonth, null, null, ct)).Where(r => r.Status != RemittanceStatus.Cancelled).ToList();
            var prepared = statements.Select(r => r.ContractId).ToHashSet();
            var commission = await remittances.AgentCommissionsAsync(lastMonth.Year, lastMonth.Month, ct);
            agencyPanel = new AgencyPanel(scope.IsRestricted, scope.AgentName, lastMonth, contracts.Count, contracts.Sum(c => c.PropertyCount),
                contracts.Where(c => !prepared.Contains(c.Id)).ToList(),
                statements.Count(r => r.Status == RemittanceStatus.Approved),
                statements.Sum(r => r.GrossCollections),
                scope.IsRestricted ? commission.Sum(r => r.AgentShare) : commission.Sum(r => r.AgencyCommission));
        }

        SalesSummaryTotals? salesTotals = null;
        var caps = await capabilities.GetAsync(ct);
        var finances = await capabilities.CanViewFinancialsAsync(ct);
        if (caps.Sales)
        {
            var saleTotals = await sales.GetTotalsAsync(ct);
            if (saleTotals.Reserved + saleTotals.Active + saleTotals.Completed > 0) salesTotals = saleTotals;
        }

        // ---- Developers: one card per development project, and a developer's checklist ----
        var isDeveloper = caps.Sales && !caps.Rentals;   // only selling: the developer's checklist
        var projects = new List<ProjectCard>();
        if (caps.Sales && finances)
        {
            foreach (var a in (await assets.ListAsync(ct)).Where(a => a.LifecycleState != AssetLifecycle.Inactive))
            {
                var o = await costing.GetOverviewAsync(a.Id, ct);
                if (o is null) continue;
                var isProject = RealEstateERP.Core.Common.PropertyRules.Effective(o.Asset.Purpose, o.Asset.Type) == PropertyPurpose.Sell;
                if (!isProject) continue;
                var overdue = (await sales.ListAsync(SalesListView.Open, null, a.Id, null, ct)).Sum(x => x.OverdueAmount);
                projects.Add(ProjectCard.From(o, overdue));
            }
        }
        var firstProject = projects.FirstOrDefault()?.AssetId;
        var allAssets = (await assets.ListAsync(ct)).Where(a => a.LifecycleState != AssetLifecycle.Inactive).ToList();
        var firstAssetId = isDeveloper ? null : allAssets.FirstOrDefault()?.Id;
        var toLet = allAssets.Where(a => RealEstateERP.Core.Common.PropertyRules.IsToLet(a.Purpose)).ToList();

        // ---- A card for every property: for sale (project) and to let (rental) ----
        var rentalCards = new List<RentalCard>();
        if (caps.Rentals && allAssets.Any(a => RealEstateERP.Core.Common.PropertyRules.IsToLet(a.Purpose)))
        {
            var money = await reports.GetPropertyMoneyAsync(ct);
            foreach (var a in allAssets.Where(a => RealEstateERP.Core.Common.PropertyRules.IsToLet(a.Purpose)))
            {
                var st = a.Stats;
                var m = money.GetValueOrDefault(a.Id);
                var setup = await estate.GetSetupAsync(a.Id, ct);
                rentalCards.Add(new RentalCard(a.Id, a.Name, a.Type, a.Location,
                    st.Available + st.Reserved + st.Occupied, st.Occupied, st.Available + st.Reserved, st.LettableRent, st.OccupiedRent,
                    m?.InvoicedThisMonth ?? 0, m?.CollectedThisMonth ?? 0, m?.Overdue ?? 0, m?.OverdueInvoices ?? 0,
                    setup.Complete ? null : setup.Next?.Title, finances ? m?.NetThisMonth ?? 0 : null));
            }
        }
        var propertyCards = projects.Select(p => new PropertyCardItem(p.Name, p, null))
            .Concat(rentalCards.Select(r => new PropertyCardItem(r.Name, null, r)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var hasEstateCharges = firstAssetId is int a0 && (await estate.GetChargesAsync(a0, ct)).Count > 0;
        var anySale = salesTotals is not null;
        IReadOnlyList<SetupStep> steps = !caps.HasProperties
            ?
            [
                new("Create your business", "Your account, team and subscription live here.", true, null, null),
                new("Add your first property", "An estate or building to let, or land to sell. Its purpose decides the menus you'll see.", false,
                    "Add property", Url.Action("Create", "Properties"))
            ]
            : isDeveloper
            ?
            [
                new("Create your business", "Your account, team and subscription live here.", true, null, null),
                new("Add a land or development project", "The land you've bought or are buying, with its size and title.", totals.AssetCount > 0,
                    "Add project", Url.Action("Create", "Properties")),
                new("Record the purchase and budget", "Land price and a budget for each cost, so you see the margin before you build.", projects.Any(p => p.HasCosts),
                    "Enter costs", firstProject is int fp ? Url.Action("Setup", "Costing", new { id = fp }) : null),
                new("Create the plots", "Subdivide into plots with sizes and prices.", projects.Any(p => p.Plots > 0),
                    "Create plots", firstProject is int fp2 ? Url.Action("Setup", "Costing", new { id = fp2 }) : null),
                new("Record your first sale", "Reserve a plot for a buyer with a payment plan.", anySale,
                    "New sale", projects.Any(p => p.Available > 0) ? Url.Action("Create", "Sales") : null)
            ]
            :
            [
                new("Create your business", "Your account, team and subscription live here.", true, null, null),
                new("Add a property and its units", "Buildings, blocks, floors and units with their rent.", hasUnits,
                    totals.AssetCount == 0 ? "Add property" : "Add units",
                    totals.AssetCount == 0 ? Url.Action("Create", "Properties") : Url.Action("Index", "Properties")),
                new("Set the estate's charges", "Garbage, security, water... on each property's page. Leases get them automatically.", hasEstateCharges,
                    "Set charges", firstAssetId is int fa ? Url.Action("Details", "Properties", new { id = fa }) + "#estate-charges" : null),
                new("Add tenants and leases", "Link each tenant to a unit with their lease terms.", hasLeases,
                    "Create a lease", hasUnits ? Url.Action("Create", "Leases") : null),
                new("Send your first invoices", "Bill every active lease for the month in one go.", billing.IssuedCount > 0,
                    "Generate invoices", hasLeases ? Url.Action("Generate", "Invoices") : null)
            ];

        return View(new DashboardViewModel
        {
            Agency = agencyPanel,
            Projects = projects,
            PropertyCards = propertyCards,
            ToLetCount = toLet.Count,
            NewLeads = await listings.CountNewLeadsAsync(ct),
            AwaitingConfirmation = (await listings.GetAwaitingConfirmationAsync(null, ct)).Count,
            RentalStats = toLet.Count == 0 ? null : new UnitStats(
                toLet.Sum(a => a.Stats.Available), toLet.Sum(a => a.Stats.Reserved), toLet.Sum(a => a.Stats.Occupied),
                toLet.Sum(a => a.Stats.Sold), toLet.Sum(a => a.Stats.Inactive), toLet.Sum(a => a.Stats.LettableRent), toLet.Sum(a => a.Stats.OccupiedRent)),
            SetupTitle = !caps.HasProperties ? "Get started" : isDeveloper ? "Set up your project" : "Set up your portfolio",
            ShowRentalSummary = caps.Rentals,
            Sales = salesTotals,
            Snapshot = snapshot,
            Totals = totals,
            NowUtc = clock.GetUtcNow().UtcDateTime,
            Today = RealEstateERP.Core.Common.KenyaTime.TodayEat(clock),
            CanManageSubscription = currentUser.Role is BusinessRole.Owner or BusinessRole.Admin,
            CanEdit = currentUser.Role is not BusinessRole.Viewer,
            LeaseCounts = counts,
            Billing = billing,
            Collections = await payments.GetSummaryAsync(ct),
            NeedingAttention = await leases.GetNeedingAttentionAsync(30, ct),
            Steps = steps
        });
    }
}
