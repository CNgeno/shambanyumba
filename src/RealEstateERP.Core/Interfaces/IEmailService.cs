namespace RealEstateERP.Core.Interfaces;

/// <param name="ReplyTo">Optional: replies go here (e.g. the landlord's email) instead of the platform sender.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? ReplyTo = null);

/// <summary>
/// LOW-LEVEL. Sends email through SMTP, using the Admin > Settings values when Email.SmtpHost is set, else the
/// appsettings "EmailSettings" section. With neither: the Development mailbox (/dev/mailbox) in Development,
/// or a logged warning in Production. Checked on every send, so settings changes apply immediately.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends now. Throws if the mail server refuses (callers decide whether that matters).</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>Plain-language description of where email currently goes (shown on the admin settings page).</summary>
    Task<string> DescribeAsync(CancellationToken ct = default) => Task.FromResult("Custom email service");
}

public sealed record DevEmail(int Id, DateTime SentAtUtc, string To, string Subject, string HtmlBody);

/// <summary>Development only: an in-memory list of the most recent emails the app "sent".</summary>
public interface IDevMailbox
{
    IReadOnlyList<DevEmail> All();
    DevEmail? Get(int id);
    void Clear();
}
