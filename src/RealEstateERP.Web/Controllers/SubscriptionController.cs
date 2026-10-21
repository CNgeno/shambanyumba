using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Must stay reachable when suspended, otherwise nobody could renew.</summary>
[SkipSubscriptionCheck]
public class SubscriptionController(
    ISubscriptionService subscriptions,
    ISystemConfigService config,
    ICurrentUserService currentUser,
    IFileStorage storage,
    TimeProvider clock) : Controller
{
    private int BusinessId => currentUser.BusinessId!.Value;   // guaranteed by TenantResolutionMiddleware
    private bool CanRenew => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new SubscriptionIndexViewModel
        {
            Snapshot = await subscriptions.GetSnapshotAsync(BusinessId, ct),
            Payments = await subscriptions.GetPaymentsAsync(BusinessId, ct),
            CanRenew = CanRenew,
            NowUtc = clock.GetUtcNow().UtcDateTime
        });

    [HttpGet]
    public async Task<IActionResult> Renew(int? planId, CancellationToken ct)
        => View(await BuildRenewPageAsync(new RenewInput { PlanId = planId }, ct));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Renew([Bind(Prefix = "Input")] RenewInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(await BuildRenewPageAsync(input, ct));

        UploadedFile? proof = null;
        if (input.Proof is { Length: > 0 } file)
        {
            var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            proof = new UploadedFile(buffer, file.FileName, file.Length);
        }

        var result = await subscriptions.SubmitRenewalAsync(BusinessId,
            new RenewalRequest(input.PlanId!.Value, input.Months, input.Method!.Value, input.Reference!, proof), ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(await BuildRenewPageAsync(input, ct));
        }

        TempData["Success"] = "Payment submitted. We'll verify it and extend your subscription, usually within one working day.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Proof(int id, CancellationToken ct)
    {
        var payment = await subscriptions.GetPaymentAsync(id, ct);
        if (payment is null || payment.BusinessId != BusinessId || payment.ProofStorageKey is null)
            return NotFound();

        var stream = await storage.OpenReadAsync(payment.ProofStorageKey, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(payment.ProofFileName ?? payment.ProofStorageKey));
    }

    private async Task<RenewPageViewModel> BuildRenewPageAsync(RenewInput input, CancellationToken ct)
    {
        var prefix = await config.GetAsync(ConfigKeys.MpesaAccountPrefix, ct) ?? "SB";
        return new RenewPageViewModel
        {
            Snapshot = await subscriptions.GetSnapshotAsync(BusinessId, ct),
            Plans = await subscriptions.GetAvailablePlansAsync(BusinessId, ct),
            PendingPayment = await subscriptions.GetPendingPaymentAsync(BusinessId, ct),
            CanRenew = CanRenew,
            MaxProofSizeMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct),
            Input = input,
            Instructions = new PaymentInstructions
            {
                MpesaPaybill = await config.GetAsync(ConfigKeys.MpesaPaybill, ct) ?? "",
                MpesaAccount = $"{prefix}{BusinessId}",
                BankName = await config.GetAsync(ConfigKeys.BankName, ct) ?? "",
                BankAccountName = await config.GetAsync(ConfigKeys.BankAccountName, ct) ?? "",
                BankAccountNumber = await config.GetAsync(ConfigKeys.BankAccountNumber, ct) ?? "",
                BankBranch = await config.GetAsync(ConfigKeys.BankBranch, ct) ?? "",
                BankSwiftCode = await config.GetAsync(ConfigKeys.BankSwiftCode, ct) ?? ""
            }
        };
    }
}
