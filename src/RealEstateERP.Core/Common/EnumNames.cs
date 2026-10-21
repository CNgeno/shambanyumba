using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RealEstateERP.Core.Common;

/// <summary>An enum value's [Display(Name)] (or its name), usable outside the web layer.</summary>
public static class EnumNames
{
    public static string Display(Enum value)
        => value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
}
