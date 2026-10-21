using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// The ONLY way to change Business.CreditBalance: the balance and its ledger entry move together, in the caller's
/// SaveChanges. A static helper (not a service) so SubscriptionService and CreditService can share it without
/// depending on each other. The Business must be tracked (its rowversion stops two writers racing).
/// </summary>
public static class CreditLedger
{
    public static CreditTransaction Post(Business business, int credits, CreditTransactionType type, string description, string? reference = null)
    {
        // Credits never go negative: there is no informal credit. Callers check the balance first and pause the
        // credit-priced action instead (PDF refused, lead waits for credits...); this is the backstop.
        if (business.CreditBalance + credits < 0)
            throw new InvalidOperationException($"Not enough credits: the balance is {business.CreditBalance} and this needs {-credits}.");
        business.CreditBalance += credits;
        var entry = new CreditTransaction
        {
            Business = business,
            Type = type,
            Credits = credits,
            BalanceAfter = business.CreditBalance,
            Description = description,
            Reference = reference
        };
        if (business.Id != 0) entry.BusinessId = business.Id;
        return entry;
    }
}
