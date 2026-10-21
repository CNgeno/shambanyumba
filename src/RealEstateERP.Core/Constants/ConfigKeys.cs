namespace RealEstateERP.Core.Constants;

/// <summary>Keys in the SystemConfiguration table (FR-02-D). Values are edited by the platform admin.</summary>
public static class ConfigKeys
{
    // Subscription
    public const string GraceDays = "Subscription.GraceDays";
    public const string TrialPlanCode = "Subscription.TrialPlanCode";

    // How businesses pay SmartBiz
    public const string MpesaPaybill = "Payment.MpesaPaybill";
    public const string MpesaAccountPrefix = "Payment.MpesaAccountPrefix";
    public const string BankName = "Payment.BankName";
    public const string BankAccountName = "Payment.BankAccountName";
    public const string BankAccountNumber = "Payment.BankAccountNumber";
    public const string BankBranch = "Payment.BankBranch";
    public const string BankSwiftCode = "Payment.BankSwiftCode";

    // Uploads
    public const string MaxProofSizeMb = "Upload.MaxProofSizeMb";

    // Billing defaults for businesses' own tenants (used from Sprint 4/6)
    public const string Currency = "Billing.Currency";
    public const string DefaultGraceDaysAfterDue = "Billing.DefaultGraceDaysAfterDue";
    public const string DefaultPenaltyPercent = "Billing.DefaultPenaltyPercent";

    // Credits
    public const string CreditPrice = "Credits.PricePerCredit";
    public const string CreditBundles = "Credits.BundleSizes";

    // Email (SMTP). When SmtpHost is blank, appsettings "EmailSettings" is used instead.
    public const string SmtpHost = "Email.SmtpHost";
    public const string SmtpPort = "Email.SmtpPort";
    public const string SmtpUserName = "Email.SmtpUserName";
    public const string SmtpPassword = "Email.SmtpPassword";
    public const string SmtpFromAddress = "Email.FromAddress";
    public const string SmtpFromName = "Email.FromName";
    public const string SmtpEnableSsl = "Email.EnableSsl";

    // Marketplace (verified leads)
    public const string LeadCredits = "Marketplace.LeadCredits";
    public const string LeadRefundDays = "Marketplace.LeadRefundDays";
    public const string LeadMaxRefundsPerMonth = "Marketplace.MaxRefundsPerMonth";

    // Platform
    public const string PlatformName = "Platform.Name";
    public const string SupportEmail = "Platform.SupportEmail";
    public const string SupportPhone = "Platform.SupportPhone";
}
