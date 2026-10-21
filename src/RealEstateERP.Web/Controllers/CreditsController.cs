using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-15. Reachable while suspended too, so a business can always see what it has.</summary>
[SkipSubscriptionCheck]
public class CreditsController(ICreditService credits, ISystemConfigService config, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var prefix = await config.GetAsync(ConfigKeys.MpesaAccountPrefix, ct) ?? "SB";
        return View(new CreditsViewModel
        {
            Overview = await credits.GetOverviewAsync(ct),
            CanBuy = currentUser.Role is BusinessRole.Owner or BusinessRole.Admin,
            Instructions = new PaymentInstructions
            {
                MpesaPaybill = await config.GetAsync(ConfigKeys.MpesaPaybill, ct) ?? "",
                MpesaAccount = $"{prefix}{currentUser.BusinessId}",
                BankName = await config.GetAsync(ConfigKeys.BankName, ct) ?? "",
                BankAccountName = await config.GetAsync(ConfigKeys.BankAccountName, ct) ?? "",
                BankAccountNumber = await config.GetAsync(ConfigKeys.BankAccountNumber, ct) ?? "",
                BankBranch = await config.GetAsync(ConfigKeys.BankBranch, ct) ?? ""
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Buy(BuyCreditsForm form, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await credits.RequestPurchaseAsync(new CreditPurchaseInput(form.Credits, form.Method, form.Reference), ct)
            : Core.Common.Result<int>.Fail("Enter the transaction reference.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"Purchase of {form.Credits} credits submitted. They'll be added once the payment is verified." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
