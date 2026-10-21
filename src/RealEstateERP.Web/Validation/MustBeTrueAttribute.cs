using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Web.Validation;

/// <summary>
/// A checkbox that must be ticked (e.g. accepting the terms). Checked on the server only.
/// [Range(typeof(bool), "true", "true")] must NOT be used for this: its browser-side check compares numbers,
/// so it rejects a ticked box and the form can never be submitted.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public MustBeTrueAttribute() : base("Tick the box to continue.") { }

    public override bool IsValid(object? value) => value is true;
}
