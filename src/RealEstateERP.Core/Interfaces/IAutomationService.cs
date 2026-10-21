using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// Emails invoices and receipts to tenants. Depends on: DbContext, IEmailService, ICurrentUserService, TimeProvider.
/// Email is best-effort: a failure returns a message and never undoes the invoice or payment.
/// </summary>
public interface ITenantMessagingService
{
    Task<Result> SendInvoiceAsync(int invoiceId, CancellationToken ct = default);
    Task<Result> SendReceiptAsync(int paymentId, CancellationToken ct = default);

    /// <summary>After a payment: emails the receipt if the business has that switched on and the tenant has an email.
    /// Returns a short note for the user, or null when nothing was attempted.</summary>
    Task<string?> AutoSendReceiptAsync(int paymentId, CancellationToken ct = default);
}

/// <summary>
/// ORCHESTRATOR (top level): automatic monthly invoicing, late penalties and tenant emails for the CURRENT business.
/// Depends on: IBillingRunService, IArrearsService, ITenantMessagingService, DbContext, ICurrentUserService, TimeProvider.
/// Run by the daily background job inside a business scope, or by "Run now" in Business settings.
/// </summary>
public interface IAutomationService
{
    Task<AutomationStatus> GetStatusAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(AutomationSettings settings, CancellationToken ct = default);

    /// <summary>Does whatever is due today. Safe to call repeatedly: invoices once per month, penalties once per invoice.</summary>
    Task<AutomationRunResult> RunAsync(CancellationToken ct = default);

    /// <summary>Daily job: claims today's run for the current business. False if already run today (here or on another server).</summary>
    Task<bool> TryClaimDailyRunAsync(CancellationToken ct = default);

    /// <summary>Businesses with any automation switched on (for the daily job; not limited to the current business).</summary>
    Task<IReadOnlyList<int>> GetAutomatedBusinessIdsAsync(CancellationToken ct = default);
}
