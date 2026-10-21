namespace RealEstateERP.Core.Enums;

// Enums are stored as strings in SQL Server (see ApplicationDbContext.ConfigureConventions).
// Never rename a member without a data migration.

public enum BusinessType
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Landlord")] Owner,
    Developer,
    Agency
}

public enum BusinessRole
{
    Owner,
    Admin,
    User,
    Viewer
}
