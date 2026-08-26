using System.Text;

namespace KeplerTalento.Domain.Catalogs;

/// <summary>
/// Normalization used for catalog name uniqueness. The result is persisted in
/// <see cref="CatalogItem.NameNormalized"/> and carries the unique index, so two
/// names that differ only by surrounding whitespace, casing, or accents collide.
/// </summary>
/// <remarks>
/// The accent fold is an explicit table rather than Unicode decomposition on purpose:
/// the API runs with invariant globalization (no ICU), where <c>string.Normalize</c>
/// silently returns the string unchanged. An explicit table behaves identically in
/// tests, in the container, and on any host, and needs no new runtime dependency.
/// </remarks>
public static class CatalogName
{
    private const string Accented = "áàäâãåéèëêíìïîóòöôõúùüûñçýÿšžÁÀÄÂÃÅÉÈËÊÍÌÏÎÓÒÖÔÕÚÙÜÛÑÇÝŠŽ";
    private const string Folded = "aaaaaaeeeeiiiiooooouuuuncyyszAAAAAAEEEEIIIIOOOOOUUUUNCYSZ";

    public static string Normalize(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            var index = Accented.IndexOf(character);
            builder.Append(index >= 0 ? Folded[index] : character);
        }
        return builder.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Derives a stable uppercase code from a Spanish name, matching the slug the
    /// browser-side service produced before catalogs moved to the API.
    /// </summary>
    public static string DeriveCode(string? nameEs)
    {
        var normalized = Normalize(nameEs);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_');
        }
        var collapsed = string.Join('_', builder.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length == 0 ? "ITEM" : collapsed;
    }
}
