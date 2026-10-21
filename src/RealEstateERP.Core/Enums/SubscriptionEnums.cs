namespace RealEstateERP.Core.Enums;

/// <summary>
/// Effective status. COMPUTED from dates (see SubscriptionStatusCalculator), never stored,
/// so it can never drift and no background job is needed to flip it.
/// </summary>
public enum SubscriptionStatus
{
    Trial,
    Active,
    Grace,
    Suspended,
    Cancelled   // reserved: cancellation flow comes later
}

public enum SubscriptionPaymentMethod
{
    Mpesa,
    BankTransfer
}

public enum PaymentVerificationStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ConfigValueType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    /// <summary>Stored encrypted (ASP.NET Core data protection); never shown back, only replaced or cleared.</summary>
    Secret
}
