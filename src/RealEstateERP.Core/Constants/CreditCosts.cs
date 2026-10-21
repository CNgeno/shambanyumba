namespace RealEstateERP.Core.Constants;

/// <summary>FR-15 credit prices of each action.</summary>
public static class CreditCosts
{
    public const int InvoicePdf = 1;
    public const int ReceiptPdf = 1;
    public const int Sms = 2;              // Phase 2
    public const int StatementPdf = 2;
    public const int BulkInvoiceRun = 5;
    public const int BulkInvoiceThreshold = 10;   // runs creating MORE than this many invoices
}
