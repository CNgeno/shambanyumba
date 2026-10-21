namespace RealEstateERP.Infrastructure.Data;

/// <summary>Development-only demo logins. The .test domain is reserved, so no real mail can go out.</summary>
public static class DemoAccounts
{
    public sealed record Account(string Email, string FullName, string Description);

    public const string SuperAdmin = "superadmin@smartbiz.test";
    public const string Owner = "owner@smartbiz.test";
    public const string Admin = "admin@smartbiz.test";
    public const string Staff = "staff@smartbiz.test";
    public const string Viewer = "viewer@smartbiz.test";
    public const string Agent = "agent@smartbiz.test";
    public const string Lapsed = "lapsed@smartbiz.test";
    public const string NewUser = "newuser@smartbiz.test";
    public const string James = "james@smartbiz.test";

    public static readonly IReadOnlyList<Account> All =
    [
        new(SuperAdmin, "Platform Admin", "Platform admin: approve payments, edit plans and settings"),
        new(Owner, "Wanjiku Kamau", "Owns Acacia Gardens (landlord, trial) and Savanna Homes (developer: plot sales)"),
        new(Admin, "Brian Otieno", "Admin at Acacia Gardens"),
        new(Staff, "Faith Mutua", "User at Acacia Gardens (can edit, can't renew)"),
        new(Viewer, "Kevin Njoroge", "Viewer at Acacia Gardens (read-only)"),
        new(Agent, "Amina Hassan", "Principal of Mji Property Agents: landlords, agents, contracts, owner remittances"),
        new(James, "James Kamau", "Agent at Mji Property Agents: sees only his own contracts"),
        new(Lapsed, "Peter Kiprop", "Owns Lapsed Holdings (suspended, payment awaiting approval)"),
        new(NewUser, "Grace Wambui", "No business yet: goes straight to Create business")
    ];
}
