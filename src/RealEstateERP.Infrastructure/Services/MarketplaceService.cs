using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-12 public side. Anonymous: reads live listings of every business with the TENANT filter switched off
/// (soft-delete stays on), and writes only platform-level rows (leads, credit ledger, business balance) plus a
/// direct view-count update. A listing is live while Published and its unit is Available.
/// </summary>
public sealed class MarketplaceService(ApplicationDbContext db, IEmailService email, ISystemConfigService config, TimeProvider clock) : IMarketplaceService
{
    private const int PageSize = 12;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private IQueryable<Listing> AllListings => db.Listings.IgnoreQueryFilters([QueryFilters.Tenant]);

    /// <summary>
    /// What the public sees: published listings of available units, from businesses whose subscription is usable
    /// (trial, paid or within the grace period). A suspended business's listings come back when it renews.
    /// </summary>
    private async Task<IQueryable<Listing>> LiveAsync(CancellationToken ct)
    {
        var graceDays = await config.GetIntAsync(ConfigKeys.GraceDays, 3, ct);
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-graceDays);
        return AllListings.Where(l => l.Status == ListingStatus.Published && l.Unit.Status == UnitStatus.Available
            && db.Subscriptions.Any(s => s.BusinessId == l.BusinessId && s.PeriodEndUtc >= cutoff)
            && !db.Businesses.Any(b => b.Id == l.BusinessId && b.IsDemo));   // the public demo never reaches the real marketplace
    }

    // ------------------------------------------------------------------ browsing

    public async Task<PublicSearchResult> SearchAsync(PublicSearch search, CancellationToken ct = default)
    {
        var query = await LiveAsync(ct);
        if (search.Type is ListingType t) query = query.Where(l => l.Type == t);
        if (!string.IsNullOrWhiteSpace(search.County)) query = query.Where(l => l.Unit.Asset.County == search.County);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var text = search.Text.Trim();
            query = query.Where(l => l.Title.Contains(text) || l.Unit.Asset.Name.Contains(text) || (l.Unit.Asset.Location != null && l.Unit.Asset.Location.Contains(text)));
        }
        if (search.MinPrice is decimal min) query = query.Where(l => l.Price >= min);
        if (search.MaxPrice is decimal max) query = query.Where(l => l.Price <= max);
        if (search.Bedrooms is int beds) query = query.Where(l => l.Unit.Bedrooms >= beds);
        if (search.UnitType is UnitType ut) query = query.Where(l => l.Unit.Type == ut);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, search.Page);
        var items = await Cards(query.OrderByDescending(l => l.PublishedAtUtc).ThenByDescending(l => l.Id).Skip((page - 1) * PageSize).Take(PageSize), ct);
        var counties = await (await LiveAsync(ct)).Where(l => l.Unit.Asset.County != null).Select(l => l.Unit.Asset.County!).Distinct().OrderBy(c => c).ToListAsync(ct);
        return new PublicSearchResult(items, total, page, PageSize, counties);
    }

    public async Task<IReadOnlyList<PublicListingCard>> LatestAsync(int count, CancellationToken ct = default)
        => await Cards((await LiveAsync(ct)).OrderByDescending(l => l.PublishedAtUtc).Take(count), ct);

    public async Task<PublicListingDetails?> GetAsync(int listingId, bool countView, CancellationToken ct = default)
    {
        var card = (await Cards((await LiveAsync(ct)).Where(l => l.Id == listingId), ct)).FirstOrDefault();
        if (card is null) return null;

        var extra = await (await LiveAsync(ct)).Where(l => l.Id == listingId).Select(l => new { l.Description, l.Unit.AssetId, AssetName = l.Unit.Asset.Name }).FirstAsync(ct);
        var photos = await db.ListingPhotos.IgnoreQueryFilters([QueryFilters.Tenant]).Where(p => p.ListingId == listingId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);
        var more = await Cards((await LiveAsync(ct)).Where(l => l.Unit.AssetId == extra.AssetId && l.Id != listingId).OrderBy(l => l.Price).Take(6), ct);

        if (countView)   // direct UPDATE: an anonymous visitor may not save tenant rows through the change tracker
            await AllListings.Where(l => l.Id == listingId).ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewCount, l => l.ViewCount + 1), ct);

        return new PublicListingDetails(card, extra.Description, photos, extra.AssetName, more);
    }

    public async Task<(string Key, string FileName)?> GetPublicPhotoAsync(int photoId, CancellationToken ct = default)
    {
        var photo = await db.ListingPhotos.IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(p => p.Id == photoId && p.Listing.Status == ListingStatus.Published)
            .Select(p => new { p.StorageKey, p.FileName }).FirstOrDefaultAsync(ct);
        return photo is null ? null : (photo.StorageKey, photo.FileName);
    }

    // ------------------------------------------------------------------ enquiries

    public async Task<Result<bool>> SubmitEnquiryAsync(EnquiryInput input, string verifyUrlFormat, string leadsUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length < 2) return Result<bool>.Fail("Enter your name.");
        var phone = KenyanPhone.Normalize(input.Phone);
        if (phone is null) return Result<bool>.Fail("Enter a valid phone number, e.g. 0712 345 678.");
        string address;
        try { address = new MailAddress(input.Email.Trim()).Address.ToLowerInvariant(); }
        catch (FormatException) { return Result<bool>.Fail("Enter a valid email address."); }
        if (!input.Consent) return Result<bool>.Fail("Tick the box to agree to your details being shared with the advertiser.");

        var listing = await (await LiveAsync(ct)).Where(l => l.Id == input.ListingId).Select(l => new { l.Id, l.BusinessId, l.Title }).FirstOrDefaultAsync(ct);
        if (listing is null) return Result<bool>.Fail("This property is no longer available.");

        var now = Now;

        // Confirmed in this browser within the window (and the server agrees): deliver now, no new email.
        if (input.EmailConfirmedRecently
            && await db.ListingLeads.AnyAsync(l => l.Email == address && l.VerifiedAtUtc != null
                                                   && l.VerifiedAtUtc > now.AddDays(-MarketplaceRules.ConfirmedEmailValidDays), ct))
        {
            var direct = new ListingLead { BusinessId = listing.BusinessId, ListingId = listing.Id, Name = input.Name.Trim(), Phone = phone, Email = address,
                Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim(), ConsentedAtUtc = now, VerifyBeforeUtc = now.Add(MarketplaceRules.VerifyWindow) };
            db.ListingLeads.Add(direct);
            await db.SaveChangesAsync(ct);
            await UnlockAsync(direct.Id, leadsUrl, ct);
            return Result<bool>.Ok(true);
        }

        // Re-submitting before confirming: refresh the same pending enquiry instead of piling up duplicates.
        var lead = await db.ListingLeads.FirstOrDefaultAsync(l => l.ListingId == listing.Id && l.Email == address && l.Status == LeadStatus.Unverified && l.VerifyBeforeUtc > now, ct);
        if (lead is null)
        {
            lead = new ListingLead { BusinessId = listing.BusinessId, ListingId = listing.Id };
            db.ListingLeads.Add(lead);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lead.Name = input.Name.Trim();
        lead.Phone = phone;
        lead.Email = address;
        lead.Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim();
        lead.ConsentedAtUtc = now;
        lead.VerificationTokenHash = Hash(token);
        lead.VerifyBeforeUtc = now.Add(MarketplaceRules.VerifyWindow);
        await db.SaveChangesAsync(ct);

        var platform = await config.GetAsync(ConfigKeys.PlatformName, ct) ?? "ShambaNyumba";
        try
        {
            await email.SendAsync(new EmailMessage(address, $"Confirm your enquiry: {listing.Title}",
            EmailTemplates.Render(platform, "Confirm your enquiry",
                [$"Hi {FirstName(lead.Name)}, thanks for your interest in \"{listing.Title}\".",
                 "Confirm your email address and we'll pass your details to the advertiser so they can contact you."],
                "Confirm my enquiry", string.Format(verifyUrlFormat, token),
                "The link works for 48 hours. If you didn't ask about this property, ignore this email.")), ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            // The enquiry is saved; sending again from the property page refreshes it and resends the link.
            return Result<bool>.Fail("We couldn't send the confirmation email just now. Please try again in a few minutes.");
        }
        return Result<bool>.Ok(false);
    }

    public async Task<VerifyResult> VerifyAsync(string token, string leadsUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return new VerifyResult(false, null, null, "This confirmation link isn't valid.");
        var hash = Hash(token);
        var lead = await db.ListingLeads.AsNoTracking().FirstOrDefaultAsync(l => l.VerificationTokenHash == hash, ct);
        if (lead is null) return new VerifyResult(false, null, null, "This confirmation link isn't valid. It may have been replaced by a newer one.");

        var title = await AllListings.Where(l => l.Id == lead.ListingId).Select(l => l.Title).FirstAsync(ct);
        var businessName = await db.Businesses.Where(b => b.Id == lead.BusinessId).Select(b => b.Name).FirstAsync(ct);
        if (lead.Status != LeadStatus.Unverified)
            return new VerifyResult(true, title, businessName, "You've already confirmed this enquiry. The advertiser has your details.", lead.Email);
        if (lead.VerifyBeforeUtc < Now)
            return new VerifyResult(false, title, businessName, "This link has expired. Send your enquiry again from the property page.");

        await UnlockAsync(lead.Id, leadsUrl, ct);
        return new VerifyResult(true, title, businessName,
            $"Thank you. {businessName} now has your details and will contact you. For the next {MarketplaceRules.ConfirmedEmailValidDays} days you can enquire about other properties here without confirming again.", lead.Email);
    }

    /// <summary>
    /// A confirmed enquiry becomes a lead the owner can see. Free when the same person (email or phone) already reached
    /// this owner within OwnerRepeatFreeDays (any of their properties), or this same property within RepeatWindowDays;
    /// otherwise charged, or left waiting for credits when the balance is too low (credits never go negative).
    /// Retries when the owner's balance changed at the same moment.
    /// </summary>
    private async Task UnlockAsync(int leadId, string leadsUrl, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var lead = await db.ListingLeads.FirstAsync(l => l.Id == leadId, ct);
            if (lead.Status != LeadStatus.Unverified) return;
            var title = await AllListings.Where(l => l.Id == lead.ListingId).Select(l => l.Title).FirstAsync(ct);
            var business = await db.Businesses.FirstAsync(b => b.Id == lead.BusinessId, ct);
            var now = Now;
            lead.VerifiedAtUtc = now;
            lead.VerificationTokenHash = null;   // single use

            var sameListingSince = now.AddDays(-MarketplaceRules.RepeatWindowDays);
            var sameOwnerSince = now.AddDays(-MarketplaceRules.OwnerRepeatFreeDays);
            var repeat = await db.ListingLeads.AnyAsync(l => l.Id != lead.Id
                && (l.Email == lead.Email || l.Phone == lead.Phone)
                && (l.Status == LeadStatus.Unlocked || l.Status == LeadStatus.Refunded)
                && ((l.ListingId == lead.ListingId && l.UnlockedAtUtc > sameListingSince)
                    || (l.BusinessId == lead.BusinessId && l.UnlockedAtUtc > sameOwnerSince)), ct);

            var cost = await config.GetIntAsync(ConfigKeys.LeadCredits, 10, ct);
            if (repeat)
            {
                lead.IsRepeat = true;
                lead.Status = LeadStatus.Unlocked;
                lead.UnlockedAtUtc = now;
            }
            else if (business.CreditBalance >= cost)
            {
                db.CreditTransactions.Add(CreditLedger.Post(business, -cost, CreditTransactionType.Usage, $"Lead: {title}", $"LEAD-{lead.Id}"));
                lead.Status = LeadStatus.Unlocked;
                lead.CreditsCharged = cost;
                lead.UnlockedAtUtc = now;
            }
            else
            {
                lead.Status = LeadStatus.AwaitingCredits;
            }
            NotificationLog.Add(db, lead.BusinessId, NotificationType.Lead,
                lead.Status == LeadStatus.AwaitingCredits ? $"New lead waiting for credits: {title}" : $"New lead: {lead.Name}",
                lead.Status == LeadStatus.AwaitingCredits ? "Top up credits to see their details." : $"About {title}. Call them soon.", "/Leads");

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                db.ChangeTracker.Clear();   // the business's balance changed at the same moment: re-read and retry
                continue;
            }

            await NotifyBusinessAsync(lead, title, leadsUrl, ct);
            return;
        }
    }

    // ---------------------------------------------------------------------

    private async Task NotifyBusinessAsync(ListingLead lead, string title, string leadsUrl, CancellationToken ct)
    {
        var recipients = await (from m in db.BusinessUsers.AsNoTracking()
                                join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                                where m.BusinessId == lead.BusinessId && m.IsActive && (m.Role == BusinessRole.Owner || m.Role == BusinessRole.Admin) && u.Email != null
                                select u.Email!).Distinct().ToListAsync(ct);
        if (recipients.Count == 0) return;

        var platform = await config.GetAsync(ConfigKeys.PlatformName, ct) ?? "ShambaNyumba";
        var (subject, paragraphs) = lead.Status == LeadStatus.AwaitingCredits
            ? ($"New lead waiting: {title}", new[] { $"Someone has confirmed an enquiry about \"{title}\".", "You don't have enough credits to open it. Top up your credits to see their name, phone and message." })
            : ($"New lead: {title}", new[] { $"{lead.Name} ({KenyanPhone.Display(lead.Phone)}) has confirmed an enquiry about \"{title}\".", "Call them soon: the first agent to respond usually wins." });
        foreach (var to in recipients)
        {
            try { await email.SendAsync(new EmailMessage(to, subject, EmailTemplates.Render(platform, subject, paragraphs, "Open leads", leadsUrl)), ct); }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException) { /* the lead is saved; email is best-effort */ }
        }
    }

    private async Task<IReadOnlyList<PublicListingCard>> Cards(IQueryable<Listing> query, CancellationToken ct)
    {
        var rows = await query.Select(l => new
        {
            l.Id, ListingType = l.Type, l.Title, l.Price, Location = l.Unit.Asset.Location ?? l.Unit.Asset.Name, l.Unit.Asset.County,
            UnitType = l.Unit.Type, l.Unit.Bedrooms, l.Unit.Bathrooms, l.Unit.SizeSqm,
            Cover = l.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault(),
            l.BusinessId, l.PublishedAtUtc
        }).ToListAsync(ct);

        var businessIds = rows.Select(r => r.BusinessId).Distinct().ToList();
        var names = await db.Businesses.AsNoTracking().Where(b => businessIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        return rows.Select(r => new PublicListingCard(r.Id, MarketplaceRules.Slug(r.Title), r.ListingType, r.Title, r.Price, r.Location, r.County,
            r.UnitType, r.Bedrooms, r.Bathrooms, r.SizeSqm, r.Cover, names.GetValueOrDefault(r.BusinessId, ""), r.PublishedAtUtc)).ToList();
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim().ToUpperInvariant())));
    private static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
}
