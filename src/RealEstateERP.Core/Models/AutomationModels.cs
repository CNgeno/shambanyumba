namespace RealEstateERP.Core.Models;

public sealed record AutomationSettings(
    bool AutoInvoiceEnabled, int AutoInvoiceDay, bool AutoPenaltiesEnabled, int? PenaltyGraceDays, decimal? PenaltyPercent,
    bool EmailInvoicesToTenants, bool EmailReceiptsToTenants, string? PaymentInstructions, bool AutoListVacantUnits = false);

public sealed record AutomationStatus(AutomationSettings Settings, DateOnly? LastRunOn, DateOnly? LastAutoInvoiceMonth, string? LastSummary,
    int SystemGraceDays, decimal SystemPenaltyPercent);

public sealed record AutomationRunResult(bool Ran, int InvoicesCreated, int InvoicesEmailed, int PenaltiesIssued, decimal PenaltyTotal, IReadOnlyList<string> Notes, int UnitsListed = 0)
{
    public string Summary => !Ran ? (Notes.FirstOrDefault() ?? "Nothing to do.")
        : string.Join(" ", new[]
        {
            InvoicesCreated > 0 ? $"{InvoicesCreated} invoice(s) created." : null,
            InvoicesEmailed > 0 ? $"{InvoicesEmailed} emailed to tenants." : null,
            PenaltiesIssued > 0 ? $"{PenaltiesIssued} late penalt{(PenaltiesIssued == 1 ? "y" : "ies")} issued (KES {PenaltyTotal:N0})." : null,
            UnitsListed > 0 ? $"{UnitsListed} vacant unit(s) listed for rent." : null,
            InvoicesCreated == 0 && PenaltiesIssued == 0 && UnitsListed == 0 ? "Nothing was due." : null
        }.Concat(Notes).Where(x => x is not null));
}
