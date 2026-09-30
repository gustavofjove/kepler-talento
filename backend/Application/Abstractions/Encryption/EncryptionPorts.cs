namespace KeplerTalento.Application.Abstractions.Encryption;

/// <summary>
/// Where an encrypted value lives, as <c>"&lt;Table&gt;.&lt;Column&gt;"</c> (KTL-33). It is bound
/// into every ciphertext as associated data, so a value copied into another column does not
/// decrypt there.
/// </summary>
public readonly record struct FieldContext(string Value)
{
    public static FieldContext For(string table, string column) => new($"{table}.{column}");

    public override string ToString() => Value;
}

/// <summary>
/// Encrypts candidate personal data before it reaches PostgreSQL and decrypts it on the way
/// back. Plaintext exists only inside the API process for the duration of a request.
/// </summary>
public interface IFieldProtector
{
    /// <summary>Encrypts under the active key. Never returns the input unchanged.</summary>
    string Protect(string plaintext, FieldContext context);

    /// <summary>Decrypts a value produced by <see cref="Protect"/> for the same context.</summary>
    /// <exception cref="FieldDecryptionException">
    /// The value is not an envelope, names an unknown key, or fails authentication.
    /// </exception>
    string Unprotect(string envelope, FieldContext context);

    /// <summary>
    /// Whether the value is an envelope under the active key. The backfill uses it to skip values
    /// already done and to find values still under a retired key.
    /// </summary>
    bool IsProtectedWithActiveKey(string value);
}

/// <summary>
/// Keyed hashes of normalized values, for equality lookups on encrypted columns. Only the e-mail
/// has one.
/// </summary>
public interface IBlindIndex
{
    /// <summary>The hash under the active blind-index key; what new and updated rows store.</summary>
    string Compute(string normalizedValue);

    /// <summary>
    /// The hash under every configured blind-index key, so lookups keep matching while a
    /// blind-index key is being rotated.
    /// </summary>
    IReadOnlyList<string> ComputeAll(string normalizedValue);
}

/// <summary>
/// A stored value could not be decrypted. Carries neither the value nor the key: the message
/// names only the column, which is what an operator needs to find the row.
/// </summary>
public sealed class FieldDecryptionException(FieldContext context, string reason)
    : Exception($"A stored value in {context} could not be decrypted ({reason}).")
{
    public FieldContext Context { get; } = context;

    public string Reason { get; } = reason;
}
