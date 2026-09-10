using System.Globalization;

namespace KeplerTalento.Tools.DataMigration.Validation;

/// <summary>
/// Parsers for the encodings the export contract fixes. Each returns whether the field was
/// well formed; none of them ever surfaces the value it rejected.
/// </summary>
public static class FieldParsers
{
    /// <summary>
    /// Values that look like emptiness but are not. The contract makes an empty field the
    /// only way to say "absent", so these are rejected rather than quietly treated as null —
    /// an Access export producing them means the export query is wrong.
    /// </summary>
    private static readonly string[] FalseEmpties = ["NULL", "#N/A", "N/A", "NIL", "(vacío)"];

    public static bool IsAbsent(string? value) => string.IsNullOrWhiteSpace(value);

    public static bool LooksLikeAFalseEmpty(string? value) =>
        value is not null && FalseEmpties.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool TryDate(string? value, out DateOnly? date)
    {
        date = null;
        if (IsAbsent(value))
        {
            return !LooksLikeAFalseEmpty(value);
        }
        if (!DateOnly.TryParseExact(
                value!.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }
        date = parsed;
        return true;
    }

    public static bool TryBoolean(string? value, out bool result)
    {
        result = false;
        var trimmed = (value ?? string.Empty).Trim();
        switch (trimmed)
        {
            case "true":
                result = true;
                return true;
            case "false":
                return true;
            default:
                return false;
        }
    }

    public static bool TryInteger(string? value, out int? result)
    {
        result = null;
        if (IsAbsent(value))
        {
            return !LooksLikeAFalseEmpty(value);
        }
        var trimmed = value!.Trim();
        if (!trimmed.All(char.IsAsciiDigit)
            || !int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }
        result = parsed;
        return true;
    }

    /// <summary>
    /// Deliberately permissive: this rejects text that cannot be an address, not text that
    /// is not a deliverable one. Rejecting a real candidate's unusual address would lose
    /// data the migration exists to preserve.
    /// </summary>
    public static bool IsEmail(string value)
    {
        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return false;
        }
        var domain = trimmed[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !trimmed.Any(char.IsWhiteSpace);
    }

    /// <summary>
    /// A manifest path must stay inside the export's files directory. Rejects absolute
    /// paths, drive letters, UNC prefixes and any traversal segment.
    /// </summary>
    public static bool IsSafeRelativePath(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0
            || Path.IsPathRooted(trimmed)
            || trimmed.StartsWith("\\\\", StringComparison.Ordinal)
            || trimmed.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }
        var segments = trimmed.Split(['/', '\\'], StringSplitOptions.None);
        return segments.All(segment => segment.Length > 0 && segment != "." && segment != "..");
    }
}
