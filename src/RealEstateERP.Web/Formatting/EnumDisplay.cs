using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RealEstateERP.Web.Formatting;

public static class EnumDisplay
{
    private static readonly ConcurrentDictionary<Enum, string> Cache = new();

    /// <summary>Uses [Display(Name = "...")] when present: CommercialComplex -> "Commercial complex".</summary>
    public static string Display(this Enum value) => Cache.GetOrAdd(value, v =>
        v.GetType().GetField(v.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? v.ToString());
}
