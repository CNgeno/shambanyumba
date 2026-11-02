namespace RealEstateERP.Core.Common;

/// <summary>Short-stay booking rules shared by the host and guest sides.</summary>
public static class BookingRules
{
    /// <summary>How long a guest-site booking holds its nights while the guest pays the deposit (manual payments).</summary>
    public static readonly TimeSpan DepositHold = TimeSpan.FromHours(24);
}
