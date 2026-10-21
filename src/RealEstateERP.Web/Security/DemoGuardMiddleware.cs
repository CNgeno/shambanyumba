using RealEstateERP.Core.Constants;

namespace RealEstateERP.Web.Security;

/// <summary>
/// Demo logins can't change the account (password, email, two-step, personal data), pay or renew, buy credits,
/// create businesses, invite people or change business settings. Everything else works.
/// </summary>
public sealed class DemoGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] BlockedAlways =
    [
        "/Identity/Account/Manage", "/Subscription/Renew", "/Credits/Buy", "/Business/Create", "/BusinessSettings/Team", "/BusinessSettings/Invite"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && DemoUsers.IsDemo(context.User.Identity.Name))
        {
            var path = context.Request.Path.Value ?? "";
            var blocked = BlockedAlways.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                || (HttpMethods.IsPost(context.Request.Method)
                    && (path.StartsWith("/BusinessSettings", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/Subscription", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/Credits", StringComparison.OrdinalIgnoreCase)));
            if (blocked)
            {
                context.Response.Redirect("/demo/not-in-demo");
                return;
            }
        }
        await next(context);
    }
}
