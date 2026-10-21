using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/subscriptions")]
public class AdminSubscriptionsController(
    ISubscriptionService subscriptions,
    IFileStorage storage,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new AdminSubscriptionsIndexViewModel
        {
            Pending = await subscriptions.GetPendingPaymentsAsync(ct),
            RecentlyReviewed = await subscriptions.GetRecentlyReviewedPaymentsAsync(20, ct)
        });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        var payment = await subscriptions.GetPaymentAsync(id, ct);
        if (payment is null) return NotFound();

        return View(new PaymentReviewViewModel
        {
            Payment = payment,
            BusinessSnapshot = await subscriptions.GetSnapshotAsync(payment.BusinessId, ct)
        });
    }

    [HttpPost("{id:int}/approve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? note, CancellationToken ct)
    {
        var result = await subscriptions.ApproveAsync(id, users.GetUserId(User)!, note, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Payment approved and subscription extended." : result.Error;
        return result.Succeeded ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost("{id:int}/reject"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string note, CancellationToken ct)
    {
        var result = await subscriptions.RejectAsync(id, users.GetUserId(User)!, note, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Payment rejected." : result.Error;
        return result.Succeeded ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Review), new { id });
    }

    [HttpGet("{id:int}/proof")]
    public async Task<IActionResult> Proof(int id, CancellationToken ct)
    {
        var payment = await subscriptions.GetPaymentAsync(id, ct);
        if (payment?.ProofStorageKey is null) return NotFound();

        var stream = await storage.OpenReadAsync(payment.ProofStorageKey, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(payment.ProofFileName ?? payment.ProofStorageKey));
    }
}
