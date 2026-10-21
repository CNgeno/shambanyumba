using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Infrastructure.Services.Email;

public sealed record EmailServerSettings(string Host, int Port, string? UserName, string? Password, string FromAddress, string FromName, bool EnableSsl, string Source);

/// <summary>
/// LOW-LEVEL. Decides on EVERY send where email goes, so Admin > Settings changes apply immediately:
///   1. Admin > Settings (Email.SmtpHost set): SMTP with those values (password decrypted on use).
///   2. appsettings "EmailSettings" (or the older "Email:Smtp"): SMTP. Keep the password in user-secrets / Azure settings.
///   3. Neither: the dev mailbox (/dev/mailbox) in Development; a logged warning elsewhere.
/// Depends on: ISystemConfigService (low-level), IConfiguration, IHostEnvironment, DevMailbox.
/// </summary>
public sealed class EmailService(
    ISystemConfigService settings,
    IConfiguration appConfig,
    IHostEnvironment env,
    DevMailbox mailbox,
    ILogger<EmailService> logger,
    ICurrentUserService? currentUser = null,
    RealEstateERP.Infrastructure.Data.ApplicationDbContext? db = null) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // The public demo never emails anyone: not demo logins, not tenants or buyers a visitor typed in.
        if (DemoUsers.IsDemo(message.To)
            || (currentUser?.BusinessId is int businessId && db is not null
                && await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Businesses, b => b.Id == businessId && b.IsDemo, ct)))
        {
            logger.LogInformation("Demo: email '{Subject}' not sent.", message.Subject);
            if (env.IsDevelopment()) mailbox.Add(message);
            return;
        }

        var server = await ResolveAsync(ct);
        if (server is null)
        {
            if (env.IsDevelopment())
            {
                mailbox.Add(message);
                logger.LogInformation("DEV MAILBOX: '{Subject}' to {To}. Open /dev/mailbox to read it.", message.Subject, message.To);
            }
            else
            {
                logger.LogWarning("Email NOT sent ('{Subject}' to {To}): no SMTP server is set in Admin > Settings or appsettings.", message.Subject, message.To);
            }
            return;
        }

        using var client = new SmtpClient(server.Host, server.Port)
        {
            EnableSsl = server.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(server.UserName) ? null : new NetworkCredential(server.UserName, server.Password)
        };
        using var mail = new MailMessage(new MailAddress(server.FromAddress, server.FromName), new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        if (!string.IsNullOrWhiteSpace(message.ReplyTo)) mail.ReplyToList.Add(new MailAddress(message.ReplyTo));
        await client.SendMailAsync(mail, ct);
        logger.LogInformation("Email '{Subject}' sent to {To} via {Host}.", message.Subject, message.To, server.Host);
    }

    public async Task<string> DescribeAsync(CancellationToken ct = default)
    {
        var server = await ResolveAsync(ct);
        if (server is not null)
            return $"SMTP {server.Host}:{server.Port} as {server.FromAddress} (from {server.Source})";
        return env.IsDevelopment()
            ? "No SMTP server set: emails go to the development mailbox (/dev/mailbox)"
            : "No SMTP server set: emails are NOT being sent";
    }

    private async Task<EmailServerSettings?> ResolveAsync(CancellationToken ct)
    {
        var host = await settings.GetAsync(ConfigKeys.SmtpHost, ct);
        if (!string.IsNullOrWhiteSpace(host))
        {
            var user = await settings.GetAsync(ConfigKeys.SmtpUserName, ct);
            var from = await settings.GetAsync(ConfigKeys.SmtpFromAddress, ct);
            return new EmailServerSettings(host.Trim(), await settings.GetIntAsync(ConfigKeys.SmtpPort, 587, ct), user,
                await settings.GetSecretAsync(ConfigKeys.SmtpPassword, ct),
                string.IsNullOrWhiteSpace(from) ? user ?? throw new InvalidOperationException("Set the sender address in Admin > Settings.") : from,
                await settings.GetAsync(ConfigKeys.SmtpFromName, ct) is { Length: > 0 } name ? name : "ShambaNyumba",
                await settings.GetBoolAsync(ConfigKeys.SmtpEnableSsl, true, ct),
                "Admin > Settings");
        }

        var e = appConfig.GetSection("EmailSettings");
        if (!string.IsNullOrWhiteSpace(e["Host"]))
            return new EmailServerSettings(e["Host"]!, e.GetValue("Port", 587), e["UserName"], e["Password"],
                e["FromAddress"] ?? e["UserName"] ?? throw new InvalidOperationException("EmailSettings:FromAddress is not set."),
                e["FromName"] ?? "ShambaNyumba", e.GetValue("EnableSsl", true), "appsettings EmailSettings");

        var s = appConfig.GetSection("Email:Smtp");
        if (!string.IsNullOrWhiteSpace(s["Host"]))
            return new EmailServerSettings(s["Host"]!, s.GetValue("Port", 587), s["User"], s["Password"],
                s["From"] ?? s["User"] ?? throw new InvalidOperationException("Email:Smtp:From is not set."),
                s["FromName"] ?? "ShambaNyumba", s.GetValue("EnableSsl", true), "appsettings Email:Smtp");
        return null;
    }
}

/// <summary>Development: keeps the last 100 emails in memory for /dev/mailbox.</summary>
public sealed class DevMailbox : IDevMailbox
{
    private readonly List<DevEmail> _emails = [];
    private readonly Lock _gate = new();
    private int _nextId;

    public void Add(EmailMessage m)
    {
        lock (_gate)
        {
            _emails.Insert(0, new DevEmail(++_nextId, DateTime.UtcNow, m.To, m.Subject, m.HtmlBody));
            if (_emails.Count > 100) _emails.RemoveAt(_emails.Count - 1);
        }
    }

    public IReadOnlyList<DevEmail> All() { lock (_gate) return _emails.ToList(); }
    public DevEmail? Get(int id) { lock (_gate) return _emails.FirstOrDefault(e => e.Id == id); }
    public void Clear() { lock (_gate) _emails.Clear(); }
}
