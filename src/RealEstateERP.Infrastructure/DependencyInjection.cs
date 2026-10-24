using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealEstateERP.Infrastructure.Services.Email;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                // Azure SQL resilience. Explicit transactions must then run inside
                // db.Database.CreateExecutionStrategy().ExecuteAsync(...) (used from Sprint 4).
                sql => sql.EnableRetryOnFailure()));

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        // ---- Low-level (depend on nothing but DbContext / infrastructure) ----
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<IAgentScopeService, AgentScopeService>();

        // Email: Admin > Settings SMTP, else appsettings EmailSettings, else dev mailbox (Development) / warning.
        // Scoped and resolved per send, so changes on the settings page apply immediately.
        services.AddSingleton<DevMailbox>();
        services.AddSingleton<IDevMailbox>(sp => sp.GetRequiredService<DevMailbox>());
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IPdfService, PdfService>();

        // ---- Domain (may depend on low-level only, never on each other "upward") ----
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IBusinessService, BusinessService>();   // -> ISubscriptionService, IFileStorage, ISystemConfigService
        services.AddScoped<IAssetService, AssetService>();         // -> DbContext only
        services.AddScoped<IUnitService, UnitService>();           // -> ISubscriptionService
        services.AddScoped<ITenantService, TenantService>();       // -> IFileStorage, ISystemConfigService
        services.AddScoped<ILeaseService, LeaseService>();         // -> INumberSequenceService
        services.AddScoped<IChargeService, ChargeService>();       // -> DbContext only
        services.AddScoped<IInvoiceService, InvoiceService>();     // -> INumberSequenceService
        services.AddScoped<IBillingRunService, BillingRunService>(); // -> INumberSequenceService, ICreditService
        services.AddScoped<IPaymentService, PaymentService>();     // -> INumberSequenceService (only writer of Invoice.AmountPaid)
        services.AddScoped<IStatementService, StatementService>(); // -> DbContext only (read-only)
        services.AddScoped<IArrearsService, ArrearsService>();     // -> INumberSequenceService, ISystemConfigService
        services.AddScoped<ITeamService, TeamService>();           // -> IEmailService, ISystemConfigService
        services.AddScoped<IAdminDirectoryService, AdminDirectoryService>(); // -> DbContext only (read-only)
        services.AddScoped<IAgencyService, AgencyService>();       // -> INumberSequenceService, IAgentScopeService
        services.AddScoped<IRemittanceService, RemittanceService>(); // -> INumberSequenceService, IAgentScopeService
        services.AddScoped<ITenantMessagingService, TenantMessagingService>(); // -> IEmailService
        services.AddScoped<INotificationService, NotificationService>();     // -> DbContext only
        services.AddScoped<IReportService, ReportService>();                 // -> DbContext only (read-only)
        services.AddScoped<IPlatformReportService, PlatformReportService>(); // -> ISubscriptionService (read-only)
        services.AddScoped<IAutomationService, AutomationService>();  // ORCHESTRATOR -> IBillingRunService, IArrearsService, ITenantMessagingService
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IPublicDemoService, PublicDemoService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<ITariffService, TariffService>();               // tariff bands: what each business pays           // partners, codes, commissions
        services.AddScoped<ICapabilityService, CapabilityService>();       // -> ISubscriptionService (what the business uses, from its properties)
        services.AddScoped<IEstateService, EstateService>();               // -> IFileStorage, ISystemConfigService
        services.AddScoped<IMarketplaceService, MarketplaceService>(); // -> IEmailService, ISystemConfigService (public, cross-business reads)
        services.AddScoped<IProjectCostService, ProjectCostService>(); // -> IFileStorage, ISystemConfigService (reads sales/leases)
        services.AddScoped<ISaleService, SaleService>();           // -> INumberSequenceService, IAgentScopeService, IFileStorage

        return services;
    }
}
