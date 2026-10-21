using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/businesses")]
public class AdminBusinessesController(IAdminDirectoryService directory, ISubscriptionService subscriptions) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var list = await directory.GetBusinessesAsync(q, ct);
        var rows = new List<AdminBusinessRow>(list.Count);
        foreach (var b in list)   // snapshots are cached for 60s
            rows.Add(new AdminBusinessRow(b, await subscriptions.GetSnapshotAsync(b.Id, ct)));
        return View(new AdminBusinessesViewModel { Businesses = rows, Search = q });
    }
}
