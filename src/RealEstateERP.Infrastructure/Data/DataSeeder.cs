using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>Idempotent: inserts what's missing, never overwrites values an admin has edited.</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider scopedServices, IConfiguration config)
    {
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
        await SeedPlansAsync(db);
        await SeedConfigurationAsync(db);
        await BackfillPropertyPurposeAsync(db);
        await ApplyBrandAsync(db);
        await SeedIdentityAsync(scopedServices, config);
    }

    private static async Task SeedPlansAsync(ApplicationDbContext db)
    {
        SubscriptionPlan[] plans =
        [
            new() { Code = "STARTER", Name = "Starter", Description = "For small landlords getting organised.",
                    UnitLimit = 10, MonthlyFee = 1_500m, CreditsPerMonth = 50, TrialDays = 14, SortOrder = 1 },
            new() { Code = "PROFESSIONAL", Name = "Professional", Description = "Automated billing for growing portfolios.",
                    UnitLimit = 100, MonthlyFee = 4_500m, CreditsPerMonth = 200, TrialDays = 14, SortOrder = 2 },
            new() { Code = "ENTERPRISE", Name = "Enterprise", Description = "Unlimited units for large portfolios.",
                    UnitLimit = null, MonthlyFee = 12_000m, CreditsPerMonth = 1_000, TrialDays = 14, SortOrder = 3 },
            new() { Code = "AGENT", Name = "Agent", Description = "For agencies managing properties for owners.",
                    UnitLimit = null, MonthlyFee = 3_000m, CreditsPerMonth = 150, TrialDays = 14, SortOrder = 4,
                    ForBusinessType = BusinessType.Agency }
        ];

        var existing = await db.SubscriptionPlans.Select(p => p.Code).ToListAsync();
        db.SubscriptionPlans.AddRange(plans.Where(p => !existing.Contains(p.Code)));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Properties created before purposes existed: land and every property of a former Developer business are for
    /// sale, the rest are to let. Runs once per row (only where Purpose is null), across all businesses.
    /// </summary>
    private static async Task BackfillPropertyPurposeAsync(ApplicationDbContext db)
    {
        var developers = db.Businesses.Where(b => b.Type == BusinessType.Developer).Select(b => b.Id);
        await db.Assets.IgnoreQueryFilters()
            .Where(a => a.Purpose == null && (a.Type == AssetType.Land || developers.Contains(a.BusinessId)))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Purpose, PropertyPurpose.Sell));
        await db.Assets.IgnoreQueryFilters()
            .Where(a => a.Purpose == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Purpose, PropertyPurpose.Let));
    }

    /// <summary>
    /// One-time rebrand to ShambaNyumba for databases created earlier: platform name, email sender name and support
    /// email. Recorded with a "Platform.Brand" marker so an admin's later changes are never overwritten.
    /// </summary>
    private static async Task ApplyBrandAsync(ApplicationDbContext db)
    {
        const string marker = "Platform.Brand";
        if (await db.SystemConfigurations.AnyAsync(x => x.Key == marker)) return;
        await db.SystemConfigurations.Where(x => x.Key == ConfigKeys.PlatformName)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Value, "ShambaNyumba"));
        await db.SystemConfigurations.Where(x => x.Key == ConfigKeys.SmtpFromName)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Value, "ShambaNyumba"));
        await db.SystemConfigurations.Where(x => x.Key == ConfigKeys.SupportEmail)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Value, "info@shambanyumba.com"));
        db.SystemConfigurations.Add(Setting(marker, "ShambaNyumba", ConfigValueType.Text, "Platform", "Brand applied (set automatically; don't change)."));
        await db.SaveChangesAsync();
    }

    private static async Task SeedConfigurationAsync(ApplicationDbContext db)
    {
        SystemConfiguration[] settings =
        [
            Setting(ConfigKeys.GraceDays, "3", ConfigValueType.Integer, "Subscription", "Days of access after a subscription ends before suspension."),
            Setting(ConfigKeys.TrialPlanCode, "PROFESSIONAL", ConfigValueType.Text, "Subscription", "Plan code new businesses trial on."),

            Setting(ConfigKeys.MpesaPaybill, "000000", ConfigValueType.Text, "Payment", "SmartBiz M-PESA paybill number. CHANGE BEFORE GO-LIVE."),
            Setting(ConfigKeys.MpesaAccountPrefix, "SB", ConfigValueType.Text, "Payment", "Account number shown to businesses = prefix + business ID."),
            Setting(ConfigKeys.BankName, "CHANGE ME", ConfigValueType.Text, "Payment", "Bank for subscription transfers."),
            Setting(ConfigKeys.BankAccountName, "SmartBiz Ltd", ConfigValueType.Text, "Payment", "Bank account name."),
            Setting(ConfigKeys.BankAccountNumber, "CHANGE ME", ConfigValueType.Text, "Payment", "Bank account number."),
            Setting(ConfigKeys.BankBranch, "CHANGE ME", ConfigValueType.Text, "Payment", "Bank branch."),
            Setting(ConfigKeys.BankSwiftCode, "", ConfigValueType.Text, "Payment", "SWIFT code (optional)."),

            Setting(ConfigKeys.MaxProofSizeMb, "5", ConfigValueType.Integer, "Uploads", "Maximum payment-proof file size in MB."),

            Setting(ConfigKeys.Currency, "KES", ConfigValueType.Text, "Billing", "Currency code."),
            Setting(ConfigKeys.DefaultGraceDaysAfterDue, "5", ConfigValueType.Integer, "Billing", "Default days after an invoice is due before a penalty applies."),
            Setting(ConfigKeys.DefaultPenaltyPercent, "5", ConfigValueType.Decimal, "Billing", "Default late penalty as % of the overdue balance."),

            Setting(ConfigKeys.CreditPrice, "5", ConfigValueType.Decimal, "Credits", "Price of one credit in KES."),
            Setting(ConfigKeys.CreditBundles, "100,250,500,1000", ConfigValueType.Text, "Credits", "Credit bundle sizes businesses can buy, comma separated."),

            Setting(ConfigKeys.SmtpHost, "", ConfigValueType.Text, "Email", "SMTP server, e.g. smtp-relay.brevo.com. Blank = use appsettings EmailSettings."),
            Setting(ConfigKeys.SmtpPort, "587", ConfigValueType.Integer, "Email", "SMTP port (587 with SSL/TLS is standard)."),
            Setting(ConfigKeys.SmtpUserName, "", ConfigValueType.Text, "Email", "SMTP login."),
            Setting(ConfigKeys.SmtpPassword, "", ConfigValueType.Secret, "Email", "SMTP password or key. Stored encrypted; never shown again."),
            Setting(ConfigKeys.SmtpFromAddress, "", ConfigValueType.Text, "Email", "Sender address. Must be verified with your email provider."),
            Setting(ConfigKeys.SmtpFromName, "ShambaNyumba", ConfigValueType.Text, "Email", "Sender name people see."),
            Setting(ConfigKeys.SmtpEnableSsl, "true", ConfigValueType.Boolean, "Email", "Use SSL/TLS."),

            Setting(ConfigKeys.LeadCredits, "10", ConfigValueType.Integer, "Marketplace", "Credits charged per verified lead (an enquirer who confirmed their email)."),
            Setting(ConfigKeys.LeadRefundDays, "7", ConfigValueType.Integer, "Marketplace", "Days a business has to report a fake or spam lead for a refund."),
            Setting(ConfigKeys.LeadMaxRefundsPerMonth, "5", ConfigValueType.Integer, "Marketplace", "Most lead refunds a business can get in a calendar month."),

            Setting(ConfigKeys.PlatformName, "ShambaNyumba", ConfigValueType.Text, "Platform", "Name shown to users."),
            Setting(ConfigKeys.SupportEmail, "info@shambanyumba.com", ConfigValueType.Text, "Platform", "Support email shown to users."),
            Setting(ConfigKeys.SupportPhone, "", ConfigValueType.Text, "Platform", "Support phone shown to users.")
        ];

        var existing = await db.SystemConfigurations.Select(s => s.Key).ToListAsync();
        db.SystemConfigurations.AddRange(settings.Where(s => !existing.Contains(s.Key)));
        await db.SaveChangesAsync();
    }

    private static SystemConfiguration Setting(string key, string value, ConfigValueType type, string group, string description)
        => new() { Key = key, Value = value, ValueType = type, Group = group, Description = description };

    private static async Task SeedIdentityAsync(IServiceProvider scopedServices, IConfiguration config)
    {
        var roles = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roles.RoleExistsAsync(AppRoles.SuperAdmin))
            await roles.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Platform Admin" };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Seed admin creation failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await users.IsInRoleAsync(admin, AppRoles.SuperAdmin))
            await users.AddToRoleAsync(admin, AppRoles.SuperAdmin);
    }
}
