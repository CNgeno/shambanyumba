using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// LOW-LEVEL. Must never depend on another service. Secret settings are stored encrypted with ASP.NET Core data
/// protection and are only ever returned decrypted through GetSecretAsync (GetAsync and GetAllAsync never expose them).
/// </summary>
public sealed class SystemConfigService(
    ApplicationDbContext db,
    IMemoryCache cache,
    IDataProtectionProvider? protection = null,
    ILogger<SystemConfigService>? logger = null) : ISystemConfigService
{
    private const string CacheKey = "system-config";
    private const string Purpose = "SmartBiz.SystemConfiguration.Secret.v1";

    private sealed record Entry(string Value, ConfigValueType Type);

    private async Task<IReadOnlyDictionary<string, Entry>> LoadAsync(CancellationToken ct)
        => (await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return (IReadOnlyDictionary<string, Entry>)await db.SystemConfigurations
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Key, x => new Entry(x.Value, x.ValueType), StringComparer.OrdinalIgnoreCase, ct);
        }))!;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => (await LoadAsync(ct)).TryGetValue(key, out var e) && e.Type != ConfigValueType.Secret ? e.Value : null;

    public async Task<bool> HasSecretAsync(string key, CancellationToken ct = default)
        => (await LoadAsync(ct)).TryGetValue(key, out var e) && e.Type == ConfigValueType.Secret && !string.IsNullOrEmpty(e.Value);

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        if (!(await LoadAsync(ct)).TryGetValue(key, out var e) || e.Type != ConfigValueType.Secret || string.IsNullOrEmpty(e.Value)) return null;
        if (protection is null) throw new InvalidOperationException("Data protection is not available to read secret settings.");
        try
        {
            return protection.CreateProtector(Purpose).Unprotect(e.Value);
        }
        catch (CryptographicException)
        {
            // The encryption keys changed (new machine, keys deleted): the value can't be read and must be entered again.
            logger?.LogWarning("Secret setting {Key} can't be decrypted with the current data protection keys; enter it again in Admin > Settings.", key);
            return null;
        }
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
        => int.TryParse(await GetAsync(key, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
        => decimal.TryParse(await GetAsync(key, ct), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
        => bool.TryParse(await GetAsync(key, ct), out var v) ? v : fallback;

    /// <summary>All settings for the admin page. Secret values are replaced by "" (never sent to a browser).</summary>
    public async Task<IReadOnlyList<SystemConfiguration>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await db.SystemConfigurations.AsNoTracking().OrderBy(x => x.Group).ThenBy(x => x.Key).ToListAsync(ct);
        foreach (var s in all.Where(s => s.ValueType == ConfigValueType.Secret)) s.Value = "";
        return all;
    }

    public async Task<Result> UpdateAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await db.SystemConfigurations.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null)
            return Result.Fail($"Setting '{key}' does not exist.");

        value = value.Trim();
        if (setting.ValueType == ConfigValueType.Secret)
        {
            // "" clears it; anything else is encrypted before it's stored.
            if (value.Length > 0 && protection is null) return Result.Fail("Data protection is not available to store secrets.");
            setting.Value = value.Length == 0 ? "" : protection!.CreateProtector(Purpose).Protect(value);
            await db.SaveChangesAsync(ct);
            cache.Remove(CacheKey);
            return Result.Ok();
        }

        var valid = setting.ValueType switch
        {
            ConfigValueType.Integer => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ConfigValueType.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            ConfigValueType.Boolean => bool.TryParse(value, out _),
            _ => true
        };
        if (!valid)
            return Result.Fail($"'{value}' is not a valid {setting.ValueType.ToString().ToLowerInvariant()} for {key}.");

        setting.Value = value;
        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
        return Result.Ok();
    }
}
