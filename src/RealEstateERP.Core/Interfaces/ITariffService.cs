using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Tariff bands: what a business pays, from its rent roll and instalment sales. Depends on: DbContext, ISystemConfigService.</summary>
public interface ITariffService
{
    Task<TariffQuote> QuoteAsync(int businessId, CancellationToken ct = default);
    Task<TariffTable> GetTableAsync(CancellationToken ct = default);
}
