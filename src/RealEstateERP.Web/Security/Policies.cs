namespace RealEstateERP.Web.Security;

public static class Policies
{
    /// <summary>Owner or Admin of the selected business.</summary>
    public const string BusinessAdmin = nameof(BusinessAdmin);

    /// <summary>Owner, Admin or User (everyone except Viewer).</summary>
    public const string CanEdit = nameof(CanEdit);

    /// <summary>Platform operator.</summary>
    public const string SuperAdmin = nameof(SuperAdmin);
}
