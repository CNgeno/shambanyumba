using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using RealEstateERP.Booking.Web.Services;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure;
using RealEstateERP.Infrastructure.Data;
using RealEstateERP.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Behind nginx: trust its X-Forwarded-Proto/For (only from this machine, the .NET default), so the app knows visitors
// use https: correct absolute links, secure cookies, and sign-in returns between the two sites.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);

// Shared services may be registered for things this site never uses (e.g. the main site's sign-in manager),
// so don't fail at start-up over them; each is resolved only when used.
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = false; });

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();

// One sign-in with shambanyumba.com: same key ring, same application name, same cookie (scheme and name).
// People sign in on the main site; this site reads the cookie. Settings: Auth:CookieDomain, Booking:MainSiteUrl.
builder.Services.AddDataProtection()
    .SetApplicationName(RealEstateERP.Infrastructure.TestSite.ApplicationName(builder.Configuration))
    .PersistKeysToDbContext<ApplicationDbContext>();
var mainSite = (builder.Configuration["Booking:MainSiteUrl"] ?? "https://shambanyumba.com").TrimEnd('/');
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, o =>
    {
        o.Cookie.Name = RealEstateERP.Infrastructure.TestSite.CookieName(builder.Configuration);   // must match the main site
        if (builder.Configuration["Auth:CookieDomain"] is { Length: > 0 } domain) o.Cookie.Domain = domain;
        o.Events.OnRedirectToLogin = ctx =>
        {
            var back = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
            ctx.Response.Redirect($"{mainSite}/Identity/Account/Login?ReturnUrl={Uri.EscapeDataString(back)}");
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.Redirect("/host/no-access"); return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddHostedService<HoldExpiryService>();   // releases unpaid guest-site holds

var app = builder.Build();
app.UseForwardedHeaders();   // first: everything after sees the real scheme and client address
// Test copies (Site:IsTest) are kept out of search engines.
if (RealEstateERP.Infrastructure.TestSite.IsTest(app.Configuration))
    app.Use(async (context, next) => { context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow"; await next(); });

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("Healthy"));
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// The main site applies database migrations; this site only reads and writes. Deploy the main site first.
app.Run();
