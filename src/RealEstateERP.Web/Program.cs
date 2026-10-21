using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure;
using RealEstateERP.Infrastructure.Data;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Filters;
using RealEstateERP.Web.Middleware;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddDefaultIdentity<ApplicationUser>(o =>
    {
        o.SignIn.RequireConfirmedAccount = true;   // FR-01-A
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddSignInManager<AppSignInManager>();   // keeps the selected business when Identity refreshes the cookie

// Real email for Identity's built-in pages + our account emails (confirmation, password reset).
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();
builder.Services.AddScoped<AccountEmails>();
builder.Services.AddScoped<BusinessBranding>();
builder.Services.AddHostedService<AutomationHostedService>();   // daily automatic invoicing, penalties and emails
builder.Services.AddHostedService<PublicDemoHostedService>();   // the public demo: created if missing, reset nightly

// Public enquiry form: at most 5 enquiries per visitor (IP) per 10 minutes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RealEstateERP.Web.Controllers.PropertiesPublicController.EnquiryLimit, http =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
});

// Keep the selected-business claims when Identity periodically refreshes the cookie.
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.OnRefreshingPrincipal = ctx =>
    {
        if (ctx.CurrentPrincipal is null || ctx.NewPrincipal?.Identity is not ClaimsIdentity newIdentity)
            return Task.CompletedTask;

        foreach (var type in AppClaimTypes.BusinessContext)
        {
            var claim = ctx.CurrentPrincipal.FindFirst(type);
            if (claim is not null && !newIdentity.HasClaim(c => c.Type == type))
                newIdentity.AddClaim(new Claim(claim.Type, claim.Value));
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddScoped<BusinessContextSignIn>();

builder.Services.AddAuthorizationBuilder()
    // Everything requires login unless marked [AllowAnonymous].
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.BusinessAdmin, p => p.RequireClaim(AppClaimTypes.BusinessRole,
        nameof(BusinessRole.Owner), nameof(BusinessRole.Admin)))
    .AddPolicy(Policies.CanEdit, p => p.RequireClaim(AppClaimTypes.BusinessRole,
        nameof(BusinessRole.Owner), nameof(BusinessRole.Admin), nameof(BusinessRole.User)))
    .AddPolicy(Policies.SuperAdmin, p => p.RequireRole(AppRoles.SuperAdmin));

// NFR-06: the subscription gate is global; pages opt OUT with [SkipSubscriptionCheck].
builder.Services.AddControllersWithViews(o => o.Filters.Add<SubscriptionGateFilter>());
builder.Services.AddRazorPages();

var app = builder.Build();

// Migrations (Development, with safety checks) + seeding. See DatabaseInitializer.
await DatabaseInitializer.InitializeAsync(app.Services, app.Environment, app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();   // after auth (needs the user), before authorization
app.UseAuthorization();
// Referral links (shambanyumba.com/?ref=CODE): remember the code for 60 days; it's attached when the visitor creates a business.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Query.TryGetValue("ref", out var refCode)
        && refCode.ToString() is { Length: >= 3 and <= 20 } code && code.All(char.IsLetterOrDigit))
        context.Response.Cookies.Append("sn_ref", code.ToUpperInvariant(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(60), HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps
        });
    await next();
});
app.UseMiddleware<RealEstateERP.Web.Security.DemoGuardMiddleware>();   // demo logins can't touch account, payments or settings
app.UseRateLimiter();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();   // Azure App Service health check path

app.Run();
