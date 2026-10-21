namespace RealEstateERP.Core.Common;

/// <summary>Outcome of a business operation. Expected failures (validation, rules) are results, not exceptions.</summary>
public record Result(bool Succeeded, string? Error)
{
    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}

public sealed record Result<T>(bool Succeeded, string? Error, T? Value) : Result(Succeeded, Error)
{
    public static Result<T> Ok(T value) => new(true, null, value);
    public static new Result<T> Fail(string error) => new(false, error, default);
}
