namespace RealEstateERP.Core.Common;

public static class KenyanPhone
{
    /// <summary>
    /// 0712345678 / 712345678 / 254712345678 / +254 712 345 678 -> +254712345678 (also 01xx numbers).
    /// Other international numbers starting with + are kept. Returns null if it can't be a phone number.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        static bool Mobile(char c) => c is '7' or '1';

        if (digits.Length == 10 && digits[0] == '0' && Mobile(digits[1])) return "+254" + digits[1..];
        if (digits.Length == 9 && Mobile(digits[0])) return "+254" + digits;
        if (digits.Length == 12 && digits.StartsWith("254") && Mobile(digits[3])) return "+" + digits;
        if (trimmed.StartsWith('+') && digits.Length is >= 8 and <= 15 && !digits.StartsWith("254")) return "+" + digits;
        return null;
    }

    /// <summary>+254712345678 -> 0712 345 678.</summary>
    public static string Display(string? e164)
        => e164 is { Length: 13 } && e164.StartsWith("+254")
            ? $"0{e164[4..7]} {e164[7..10]} {e164[10..]}"
            : e164 ?? string.Empty;
}
