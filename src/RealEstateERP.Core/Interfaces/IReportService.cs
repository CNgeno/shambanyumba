using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>FR-13 reports for the current business. Read-only. Depends on: DbContext, TimeProvider.</summary>
public interface IReportService
{
    /// <summary>Last <paramref name="months"/> months of collections, and revenue/occupancy for the same period.</summary>
    Task<BusinessReports> GetAsync(int months, CancellationToken ct = default);

    /// <summary>Per property (by AssetId): invoiced and collected this month, and overdue now. Properties with none are absent.</summary>
    Task<IReadOnlyDictionary<int, PropertyMoney>> GetPropertyMoneyAsync(CancellationToken ct = default);
}

/// <summary>Platform admin figures across all businesses. Read-only. Depends on: DbContext, ISubscriptionService, TimeProvider.</summary>
public interface IPlatformReportService
{
    Task<PlatformReports> GetAsync(CancellationToken ct = default);
}

/// <summary>FR-14-A notifications for the current member. Depends on: DbContext, ICurrentUserService, TimeProvider.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationRow>> RecentAsync(int take, CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}
