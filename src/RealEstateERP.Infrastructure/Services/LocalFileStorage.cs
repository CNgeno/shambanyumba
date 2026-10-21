using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Stores private files OUTSIDE wwwroot (default: {ContentRoot}/App_Data/uploads), served only
/// through authorised controller actions. Swap for Azure Blob storage before scaling out.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IHostEnvironment env, IConfiguration config)
    {
        var configured = config["Storage:LocalRoot"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string category, string extension, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var key = $"{category}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Resolve(storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}
