using System.Globalization;
using System.Text;

namespace KeplerTalento.Infrastructure.CvExtraction;

internal static class TextNormalization
{
    /// <summary>
    /// Precomposed Latin letters and the base letter they fold to.
    /// </summary>
    /// <remarks>
    /// An explicit table rather than <c>string.Normalize(FormD)</c>: the API image (Alpine, no ICU)
    /// runs .NET in globalization-invariant mode, where normalization does not decompose "Á" into
    /// "A" + accent, so "Ángela" would never match "angela" (KTL-32).
    /// </remarks>
    private static readonly Dictionary<char, char> Folds = BuildFolds(
        ("àáâãäåāăą", 'a'),
        ("çćĉċč", 'c'),
        ("ďđ", 'd'),
        ("èéêëēĕėęě", 'e'),
        ("ĝğġģ", 'g'),
        ("ĥħ", 'h'),
        ("ìíîïĩīĭįı", 'i'),
        ("ĵ", 'j'),
        ("ķ", 'k'),
        ("ĺļľŀł", 'l'),
        ("ñńņňŉ", 'n'),
        ("òóôõöøōŏő", 'o'),
        ("ŕŗř", 'r'),
        ("śŝşš", 's'),
        ("ţťŧ", 't'),
        ("ùúûüũūŭůűų", 'u'),
        ("ŵ", 'w'),
        ("ýÿŷ", 'y'),
        ("źżž", 'z'));

    /// <summary>
    /// Lower-case, accents stripped, every run of non-alphanumerics collapsed to one space. Used
    /// only for comparison, never for a value returned to the caller.
    /// </summary>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var raw in value)
        {
            // A combining accent from text that arrived already decomposed.
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            var character = char.ToLowerInvariant(raw);
            if (Folds.TryGetValue(character, out var folded))
            {
                character = folded;
            }
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }
                pendingSpace = false;
                builder.Append(character);
            }
            else
            {
                pendingSpace = true;
            }
        }
        return builder.ToString();
    }

    public static string[] Words(string value) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static Dictionary<char, char> BuildFolds(params (string Accented, char Base)[] groups)
    {
        var folds = new Dictionary<char, char>();
        foreach (var (accented, letter) in groups)
        {
            foreach (var character in accented)
            {
                folds[character] = letter;
            }
        }
        return folds;
    }
}
