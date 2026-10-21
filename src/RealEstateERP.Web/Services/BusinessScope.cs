using RealEstateERP.Core.Enums;

namespace RealEstateERP.Web.Services;

/// <summary>
/// Lets background work act as one business (no signed-in user). While a scope is open on the current async flow,
/// CurrentUserService reports that business with the Owner role, so tenant isolation, the cross-business write
/// guard and numbering all apply exactly as they do for a person.
/// </summary>
public static class BusinessScope
{
    private static readonly AsyncLocal<Context?> CurrentContext = new();

    public sealed record Context(int BusinessId, BusinessType? Type);

    public static Context? Current => CurrentContext.Value;

    public static IDisposable Begin(int businessId, BusinessType? type)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = new Context(businessId, type);
        return new Restore(previous);
    }

    private sealed class Restore(Context? previous) : IDisposable
    {
        public void Dispose() => CurrentContext.Value = previous;
    }
}
