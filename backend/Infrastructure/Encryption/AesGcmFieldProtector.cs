using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using KeplerTalento.Application.Abstractions.Encryption;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM field encryption (KTL-33 design decisions 1–2).
/// </summary>
/// <remarks>
/// A stored value is <c>ktl1.&lt;keyId&gt;.&lt;base64url(nonce ‖ ciphertext ‖ tag)&gt;</c>. The
/// nonce is 96 random bits per value, so the same plaintext never produces the same envelope,
/// and the column's <see cref="FieldContext"/> is authenticated as associated data. Keys are read
/// on first use, not at construction, so the <c>--migrate</c> entry point can build the EF model
/// without them.
/// </remarks>
public sealed class AesGcmFieldProtector(Func<FieldKeySet> keys) : IFieldProtector
{
    public const string Prefix = "ktl1.";
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly Lazy<FieldKeySet> _keys = new(keys, LazyThreadSafetyMode.ExecutionAndPublication);

    // One cipher per key per thread. Building an AesGcm creates a native key handle, which costs
    // more than encrypting a field; a text search decrypts tens of thousands of values, so the
    // handle is built once and reused. AesGcm instances are not safe to share across threads.
    private readonly ThreadLocal<Dictionary<string, AesGcm>> _ciphers = new(() => new(StringComparer.Ordinal));

    private AesGcm Cipher(string keyId, byte[] key)
    {
        var ciphers = _ciphers.Value!;
        if (!ciphers.TryGetValue(keyId, out var cipher))
        {
            cipher = new AesGcm(key, TagBytes);
            ciphers[keyId] = cipher;
        }
        return cipher;
    }

    public FieldKeySet Keys => _keys.Value;

    /// <summary>Whether the value is shaped like an envelope. Says nothing about its key or integrity.</summary>
    public static bool IsEnvelope(string? value) => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext, FieldContext context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var ring = Keys.Encryption;
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceBytes + plain.Length + TagBytes];
        var nonce = payload.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);
        Cipher(ring.ActiveId, ring.ActiveKey).Encrypt(
            nonce,
            plain,
            payload.AsSpan(NonceBytes, plain.Length),
            payload.AsSpan(NonceBytes + plain.Length, TagBytes),
            AssociatedData(context));
        CryptographicOperations.ZeroMemory(plain);
        return $"{Prefix}{ring.ActiveId}.{Base64Url.EncodeToString(payload)}";
    }

    public string Unprotect(string envelope, FieldContext context)
    {
        if (!TryParse(envelope, out var keyId, out var encoded))
        {
            throw new FieldDecryptionException(context, "not an envelope");
        }
        if (!Keys.Encryption.Keys.TryGetValue(keyId, out var key))
        {
            throw new FieldDecryptionException(context, "unknown key");
        }
        byte[] payload;
        try
        {
            payload = Base64Url.DecodeFromChars(encoded);
        }
        catch (FormatException)
        {
            throw new FieldDecryptionException(context, "malformed payload");
        }
        if (payload.Length < NonceBytes + TagBytes)
        {
            throw new FieldDecryptionException(context, "malformed payload");
        }
        var cipherLength = payload.Length - NonceBytes - TagBytes;
        var plain = new byte[cipherLength];
        try
        {
            Cipher(keyId, key).Decrypt(
                payload.AsSpan(0, NonceBytes),
                payload.AsSpan(NonceBytes, cipherLength),
                payload.AsSpan(NonceBytes + cipherLength, TagBytes),
                plain,
                AssociatedData(context));
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new FieldDecryptionException(context, "authentication failed");
        }
        var text = Encoding.UTF8.GetString(plain);
        CryptographicOperations.ZeroMemory(plain);
        return text;
    }

    public bool IsProtectedWithActiveKey(string value) =>
        TryParse(value, out var keyId, out _) && string.Equals(keyId, Keys.Encryption.ActiveId, StringComparison.Ordinal);

    /// <summary>The key identifier an envelope names, or null when the value is not an envelope.</summary>
    public static string? KeyIdOf(string? value) => value is not null && TryParse(value, out var keyId, out _) ? keyId : null;

    private static bool TryParse(string value, out string keyId, out ReadOnlySpan<char> encoded)
    {
        keyId = string.Empty;
        encoded = default;
        if (!IsEnvelope(value))
        {
            return false;
        }
        var rest = value.AsSpan(Prefix.Length);
        var dot = rest.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }
        keyId = rest[..dot].ToString();
        encoded = rest[(dot + 1)..];
        return encoded.Length > 0;
    }

    private static byte[] AssociatedData(FieldContext context) => Encoding.UTF8.GetBytes(context.Value);
}
