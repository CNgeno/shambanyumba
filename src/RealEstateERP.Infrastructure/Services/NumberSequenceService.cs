using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// LOW-LEVEL. Optimistic concurrency (rowversion) plus retry: two simultaneous requests can never get
/// the same number. Depends on the DbContext only.
/// </summary>
public sealed class NumberSequenceService(ApplicationDbContext db) : INumberSequenceService
{
    private const int MaxAttempts = 5;

    public async Task<long> NextAsync(string key, CancellationToken ct = default)
    {
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "NumberSequenceService.NextAsync must be called before other changes are added to the context.");

        for (var attempt = 1; ; attempt++)
        {
            var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (seq is null)
            {
                seq = new NumberSequence { Key = key, NextValue = 1 };
                db.NumberSequences.Add(seq);
            }

            var value = seq.NextValue;
            seq.NextValue = value + 1;

            try
            {
                await db.SaveChangesAsync(ct);
                return value;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Someone else took this number (concurrency) or created the row first (unique key). Retry.
                db.Entry(seq).State = EntityState.Detached;
            }
        }
    }
}
