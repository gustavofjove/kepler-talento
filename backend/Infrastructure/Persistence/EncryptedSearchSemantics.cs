namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// The text match and name order PostgreSQL applied before KTL-33, reproduced in the API now that
/// the fields are ciphertext. Measured facts behind each rule: <c>docs/ktl-33/design-notes.md</c>.
/// </summary>
public static class EncryptedSearchSemantics
{
    /// <summary>
    /// <c>ILIKE '%term%'</c> with the term escaped: a literal, case-insensitive, accent-sensitive
    /// substring match, folding character by character with no expansions.
    /// </summary>
    public static bool Contains(string? value, string foldedTerm) =>
        value is not null && Fold(value).Contains(foldedTerm, StringComparison.Ordinal);

    /// <summary>
    /// Lower-cases the way the database's libc (musl) did: invariant simple case mapping, plus
    /// U+0130 (<c>İ</c>) to <c>i</c>, which musl folds and .NET does not.
    /// </summary>
    public static string Fold(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                span[index] = character == 'İ' ? 'i' : char.ToLowerInvariant(character);
            }
        });

    /// <summary>
    /// <c>ORDER BY</c> under the database's collation: code-point order, upper case before lower
    /// case, accented letters after <c>z</c>.
    /// </summary>
    public static readonly StringComparer NameOrder = StringComparer.Ordinal;

    /// <summary>
    /// PostgreSQL orders <c>uuid</c> bytewise, which is the ordinal order of its canonical text.
    /// </summary>
    public static int CompareIdentifiers(Guid left, Guid right) =>
        string.CompareOrdinal(left.ToString("D"), right.ToString("D"));
}
