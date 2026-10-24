using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>What a tariff band measures.</summary>
public enum TariffMetric
{
    /// <summary>Monthly rent of let units (active leases), excluding water and other charges.</summary>
    [Display(Name = "Monthly rent roll")] RentRoll,
    /// <summary>Plot or house sales still being paid off (reserved or active, with a balance).</summary>
    [Display(Name = "Instalment accounts")] SaleAccounts
}
