using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record PropertyChargeRow(
    int Id, int ChargeDefinitionId, string Name, ChargeType Type, ChargeFrequency Frequency,
    decimal? Amount, decimal? UnitRate, string? UnitOfMeasure, int LeasesMissing)
{
    public bool IsMetered => Type == ChargeType.Utility;
}

public sealed record PropertyChargeInput(int ChargeDefinitionId, decimal? Amount, decimal? UnitRate);

public sealed record PhotoRow(int Id, string FileName, string? Caption);

public sealed record SetupStepStatus(string Key, string Title, string Description, bool Done, bool Optional);

/// <summary>The estate's guided setup: what's done and what's next, in order.</summary>
public sealed record EstateSetup(IReadOnlyList<SetupStepStatus> Steps)
{
    public int DoneCount => Steps.Count(s => s.Done);
    public int RequiredCount => Steps.Count(s => !s.Optional);
    public bool Complete => Steps.Where(s => !s.Optional).All(s => s.Done);
    public SetupStepStatus? Next => Steps.FirstOrDefault(s => !s.Done && !s.Optional);
}
