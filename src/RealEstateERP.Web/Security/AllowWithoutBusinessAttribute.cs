namespace RealEstateERP.Web.Security;

/// <summary>
/// Marks controllers/actions that a signed-in user may reach before choosing a business
/// (business selection, business creation, error page). Everything else requires one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutBusinessAttribute : Attribute;
