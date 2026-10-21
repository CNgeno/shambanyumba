using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

/// <summary>Everything the property page needs for a rental estate's setup, charges and photos.</summary>
public sealed record EstatePanelModel(
    int AssetId, EstateSetup Setup, IReadOnlyList<PropertyChargeRow> Charges, IReadOnlyList<ChargeDefinition> AvailableCharges,
    IReadOnlyList<PhotoRow> Photos, string? Description, bool CanEdit,
    PropertyMoney? Money = null, bool CanSeeFinances = false);

public sealed record UnitPhotosModel(int UnitId, string UnitNumber, int AssetId, string AssetName, IReadOnlyList<PhotoRow> Photos, bool CanEdit);
