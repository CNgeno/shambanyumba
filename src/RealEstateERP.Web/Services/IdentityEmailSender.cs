using Microsoft.AspNetCore.Identity.UI.Services;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Services;

/// <summary>Lets ASP.NET Identity's built-in pages (change email, resend confirmation) send through IEmailService.</summary>
public sealed class IdentityEmailSender(IEmailService email, ISystemConfigService config) : IEmailSender
{
    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        var platform = await config.GetAsync(ConfigKeys.PlatformName) ?? "ShambaNyumba";
        await email.SendAsync(new EmailMessage(to, subject, EmailTemplates.Wrap(platform, subject, htmlMessage)));
    }
}
