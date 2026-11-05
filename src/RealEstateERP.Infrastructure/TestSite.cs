using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace RealEstateERP.Infrastructure;

/// <summary>
/// Settings that make a copy of the site safe to run beside live (test.shambanyumba.com, testbooking.shambanyumba.com):
///   Site:IsTest = true          banner on every page, no search indexing, emails captured instead of sent
///   Auth:CookieName             its own sign-in cookie, so test and live sign-ins never mix on .shambanyumba.com
///   DataProtection:ApplicationName  its own encryption purpose: live cookies and tokens mean nothing on test
/// Live sets none of these and behaves as before.
/// </summary>
public static class TestSite
{
    public const string DefaultApplicationName = "ShambaNyumba";
    public const string DefaultCookieName = ".AspNetCore.Identity.Application";

    public static bool IsTest(IConfiguration config) => config.GetValue<bool>("Site:IsTest");

    /// <summary>Emails go to the in-memory mailbox, never SMTP: always in Development and on test sites.</summary>
    public static bool CapturesEmail(IConfiguration config, IHostEnvironment env) => env.IsDevelopment() || IsTest(config);

    public static string ApplicationName(IConfiguration config) => config["DataProtection:ApplicationName"] is { Length: > 0 } n ? n : DefaultApplicationName;
    public static string CookieName(IConfiguration config) => config["Auth:CookieName"] is { Length: > 0 } n ? n : DefaultCookieName;
}
