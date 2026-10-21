namespace RealEstateERP.Web.Security;

/// <summary>
/// Opt OUT of the subscription gate. Only for the renewal pages themselves and platform-admin pages.
/// The gate is global (NFR-06), so forgetting an attribute can never leave a page unprotected.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipSubscriptionCheckAttribute : Attribute;
