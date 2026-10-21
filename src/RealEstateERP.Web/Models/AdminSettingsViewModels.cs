using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Web.Models;

public sealed class EmailSettingsForm
{
    [StringLength(200), Display(Name = "SMTP server")] public string? Host { get; set; }
    [Range(1, 65535)] public int Port { get; set; } = 587;
    [StringLength(200), Display(Name = "Login")] public string? UserName { get; set; }
    /// <summary>Write-only: blank keeps the saved password.</summary>
    [StringLength(500), DataType(DataType.Password), Display(Name = "Password / SMTP key")] public string? NewPassword { get; set; }
    public bool ClearPassword { get; set; }
    [EmailAddress, StringLength(256), Display(Name = "Sender address")] public string? FromAddress { get; set; }
    [StringLength(100), Display(Name = "Sender name")] public string? FromName { get; set; }
    [Display(Name = "Use SSL/TLS")] public bool EnableSsl { get; set; } = true;
}

public sealed class AdminSettingsViewModel
{
    public required IReadOnlyList<SystemConfiguration> Settings { get; init; }
    public required EmailSettingsForm Email { get; init; }
    public required bool HasPassword { get; init; }
    public required string Transport { get; init; }
    public string? AdminEmail { get; init; }
}
