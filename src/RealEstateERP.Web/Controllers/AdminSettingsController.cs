using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/settings")]
public class AdminSettingsController(ISystemConfigService config, IEmailService email) : Controller
{
    private const string EmailGroup = "Email";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new AdminSettingsViewModel
        {
            Settings = (await config.GetAllAsync(ct)).Where(s => s.Group != EmailGroup).ToList(),
            Email = new EmailSettingsForm
            {
                Host = await config.GetAsync(ConfigKeys.SmtpHost, ct),
                Port = await config.GetIntAsync(ConfigKeys.SmtpPort, 587, ct),
                UserName = await config.GetAsync(ConfigKeys.SmtpUserName, ct),
                FromAddress = await config.GetAsync(ConfigKeys.SmtpFromAddress, ct),
                FromName = await config.GetAsync(ConfigKeys.SmtpFromName, ct),
                EnableSsl = await config.GetBoolAsync(ConfigKeys.SmtpEnableSsl, true, ct)
            },
            HasPassword = await config.HasSecretAsync(ConfigKeys.SmtpPassword, ct),
            Transport = await email.DescribeAsync(ct),
            AdminEmail = User.Identity?.Name
        });

    [HttpPost("update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string? value, CancellationToken ct)
    {
        var result = await config.UpdateAsync(key, value ?? string.Empty, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"{key} saved." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("email"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(EmailSettingsForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
            return RedirectToAction(nameof(Index));
        }
        if (!string.IsNullOrWhiteSpace(form.Host) && string.IsNullOrWhiteSpace(form.FromAddress) && string.IsNullOrWhiteSpace(form.UserName))
        {
            TempData["Error"] = "Enter the sender address.";
            return RedirectToAction(nameof(Index));
        }

        var updates = new List<(string Key, string Value)>
        {
            (ConfigKeys.SmtpHost, form.Host ?? ""),
            (ConfigKeys.SmtpPort, form.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            (ConfigKeys.SmtpUserName, form.UserName ?? ""),
            (ConfigKeys.SmtpFromAddress, form.FromAddress ?? ""),
            (ConfigKeys.SmtpFromName, form.FromName ?? ""),
            (ConfigKeys.SmtpEnableSsl, form.EnableSsl ? "true" : "false")
        };
        if (form.ClearPassword) updates.Add((ConfigKeys.SmtpPassword, ""));
        else if (!string.IsNullOrEmpty(form.NewPassword)) updates.Add((ConfigKeys.SmtpPassword, form.NewPassword));

        foreach (var (key, value) in updates)
        {
            var result = await config.UpdateAsync(key, value, ct);
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Index));
            }
        }
        TempData["Success"] = string.IsNullOrWhiteSpace(form.Host)
            ? "Email settings saved. With no SMTP server here, appsettings EmailSettings is used if present."
            : "Email settings saved. Send a test email to check them.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("email/test"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TestEmail(string to, CancellationToken ct)
    {
        try
        {
            var address = new MailAddress(to.Trim()).Address;
            var platform = await config.GetAsync(ConfigKeys.PlatformName, ct) ?? "ShambaNyumba";
            await email.SendAsync(new EmailMessage(address, $"{platform} test email",
                EmailTemplates.Render(platform, "Your email settings work", [$"This test was sent through: {await email.DescribeAsync(ct)}."])), ct);
            TempData["Success"] = $"Test email handed to the mail server for {address}. Check the inbox (and spam folder).";
        }
        catch (FormatException)
        {
            TempData["Error"] = "Enter a valid email address to send the test to.";
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or System.Net.Sockets.SocketException or IOException)
        {
            // Show the server's own message: it's what explains a rejected sender, bad login or blocked port.
            var detail = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
            TempData["Error"] = $"Sending failed: {detail}";
        }
        return RedirectToAction(nameof(Index));
    }
}
