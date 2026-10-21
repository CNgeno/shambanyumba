namespace RealEstateERP.Core.Interfaces;

/// <summary>Private file storage (payment proofs, KYC documents). Local disk now, Azure Blob later.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string category, string extension, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
}
