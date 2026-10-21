using RealEstateERP.Web.Security;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-13 reports for the current business.</summary>
[RequireFinancials]
public class ReportsController(IReportService reports) : Controller
{
    public async Task<IActionResult> Index(int months = 12, CancellationToken ct = default)
    {
        ViewData["Months"] = months;
        return View(await reports.GetAsync(months, ct));
    }
}
