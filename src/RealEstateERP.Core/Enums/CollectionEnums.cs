using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>FR-08-A.</summary>
public enum PaymentMethod
{
    [Display(Name = "M-PESA")] Mpesa,
    [Display(Name = "Bank transfer")] BankTransfer,
    Cheque,
    Cash
}

/// <summary>NFR-07: a payment is never edited or deleted, only reversed.</summary>
public enum PaymentStatus
{
    Active,
    Reversed
}

public enum AllocationMode
{
    [Display(Name = "Oldest invoices first")] OldestFirst,
    [Display(Name = "Choose invoices")] Manual,
    [Display(Name = "Keep as credit")] KeepAsCredit
}

public enum RefundKind
{
    [Display(Name = "Deposit refund")] Deposit,
    [Display(Name = "Overpayment refund")] Overpayment
}

public enum StatementEntryType
{
    Invoice,
    Payment,
    Reversal,
    WriteOff,
    Refund,
    DepositRefund
}
