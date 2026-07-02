namespace CureFlow.Application.Common;

public static class EnumParseHelper
{
    /// <summary>Parses snake_case or PascalCase enum strings (e.g. new_inquiry → NewInquiry).</summary>
    public static bool TryParseSnakeCase<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (Enum.TryParse<TEnum>(value, true, out result)) return true;

        var pascal = string.Concat(value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

        return Enum.TryParse<TEnum>(pascal, true, out result);
    }
}
