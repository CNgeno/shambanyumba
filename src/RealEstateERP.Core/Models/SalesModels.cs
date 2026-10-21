using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record BuyerInput(OwnerType Type, string FullName, string? IdNumber, string? KraPin, string? Phone, string? Email, string? PostalAddress, string? Notes);

public sealed record BuyerSummary(int Id, string FullName, OwnerType Type, string? Phone, string? Email, int Sales, decimal Balance);

public sealed record SaleInput(
    int BuyerId, int UnitId, int? AgentId, DateOnly SaleDate, decimal Price, PaymentPlan PaymentPlan, decimal DepositAmount,
    int InstalmentCount, InstalmentFrequency InstalmentFrequency, DateOnly? FirstInstalmentDate, decimal CommissionPercent, string? Notes);

public sealed record SaleSummary(
    int Id, string SaleNumber, int BuyerId, string BuyerName, int UnitId, string UnitNumber, int AssetId, string AssetName,
    string? AgentName, DateOnly SaleDate, decimal Price, decimal AmountPaid, SaleStatus Status, int OverdueInstalments, decimal OverdueAmount)
{
    public decimal Balance => Price - AmountPaid;
}

public sealed record InstalmentRow(int Id, int Number, string Description, DateOnly DueDate, decimal Amount, decimal AmountPaid, InstalmentState State)
{
    public decimal Balance => Amount - AmountPaid;
}

public sealed record SalePaymentRow(
    int Id, string ReceiptNumber, DateOnly PaymentDate, decimal Amount, PaymentMethod Method, string? Reference, PaymentStatus Status, string? ReversalReason);

public sealed record SaleDetails(
    Sale Sale, IReadOnlyList<InstalmentRow> Schedule, IReadOnlyList<SaleMilestone> Milestones, IReadOnlyList<SalePaymentRow> Payments, decimal OverdueAmount);

public sealed record SalePaymentInput(DateOnly PaymentDate, decimal Amount, PaymentMethod Method, string? Reference, string? Notes);

public sealed record SellableUnit(int Id, string UnitNumber, int AssetId, string AssetName, decimal? SalePrice, UnitStatus Status);

public sealed record SalesSummaryTotals(int Reserved, int Active, int Completed, decimal SoldValue, decimal Collected, decimal Outstanding, decimal OverdueAmount, int OverdueSales, decimal CollectedThisMonth);

public sealed record InventoryRow(int AssetId, string AssetName, int Units, int Available, int Reserved, int Sold, decimal AvailableValue);

public sealed record ArrearsSaleRow(SaleSummary Sale, DateOnly OldestDue, int DaysOverdue, AgingTotals Aging);

public sealed record SalesCommissionRow(int? AgentId, string AgentName, decimal SharePercent, decimal Collected, decimal Commission, decimal AgentShare);
