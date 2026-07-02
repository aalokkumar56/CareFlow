namespace CureFlow.Infrastructure.External;

public static class WhatsappPhoneHelper
{
    /// <summary>
    /// Canonical storage/API format: digits only with India country code (e.g. 919876543210).
    /// Strips +, spaces, leading 00, and redundant leading zeros.
    /// Adds 91 prefix for 10-digit local numbers.
    /// </summary>
    public static string Normalize(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];

        digits = digits.TrimStart('0');

        if (digits.Length == 10)
            digits = "91" + digits;

        return digits;

    }

    /// <summary>WhatsBiz and Meta Cloud APIs expect digits without + prefix.</summary>
    public static string ForApi(string phone) => Normalize(phone);

    /// <summary>Display format with + prefix for UI (e.g. +91 98765 43210).</summary>
    public static string ToDisplay(string phone)
    {
        var digits = Normalize(phone);

        if (string.IsNullOrWhiteSpace(digits)) return string.Empty;

        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            return $"+91 {digits[2..7]} {digits[7..]}";

        return $"+{digits}";
    }
}

