using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/reports")]
public class AdminReportsController(IPlatformReportService reports) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await reports.GetAsync(ct));
}
