using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Services;

/// <summary>The current business's details plus logo bytes, for PDFs.</summary>
public sealed class BusinessBranding(IBusinessService businesses, IFileStorage storage, ICurrentUserService currentUser)
{
    public async Task<BusinessProfile> ForPdfAsync(CancellationToken ct)
    {
        var profile = await businesses.GetProfileAsync(currentUser.BusinessId!.Value, ct)
            ?? throw new InvalidOperationException("Business not found.");
        if (profile.LogoKey is null) return profile;

        await using var stream = await storage.OpenReadAsync(profile.LogoKey, ct);
        if (stream is null) return profile;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return profile with { Logo = buffer.ToArray() };
    }
}
