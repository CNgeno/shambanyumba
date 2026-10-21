using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Low-level service: depends on the DbContext, cache and data protection (for Secret settings).</summary>
public interface ISystemConfigService
{
    /// <summary>The value, or null. Always null for Secret settings: use GetSecretAsync.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    /// <summary>Decrypted value of a Secret setting; null if blank or if it can no longer be decrypted.</summary>
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
    Task<bool> HasSecretAsync(string key, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    Task<IReadOnlyList<SystemConfiguration>> GetAllAsync(CancellationToken ct = default);
    Task<Result> UpdateAsync(string key, string value, CancellationToken ct = default);
}
