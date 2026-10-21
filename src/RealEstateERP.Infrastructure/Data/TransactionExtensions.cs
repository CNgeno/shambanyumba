using Microsoft.EntityFrameworkCore;

namespace RealEstateERP.Infrastructure.Data;

public static class TransactionExtensions
{
    /// <summary>
    /// Runs work that needs SEVERAL SaveChanges calls as one transaction. Uses the execution strategy,
    /// which is required because EnableRetryOnFailure is on. Throwing (or not committing) rolls back.
    /// </summary>
    public static Task<T> InTransactionAsync<T>(this ApplicationDbContext db, Func<Task<T>> work, CancellationToken ct = default)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
}
