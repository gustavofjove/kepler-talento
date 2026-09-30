using System.Security.Cryptography;
using System.Text;
using KeplerTalento.Application.Abstractions.Encryption;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// HMAC-SHA256 blind index, stored as <c>&lt;keyId&gt;.&lt;hex&gt;</c> (KTL-33 design decision 5).
/// </summary>
/// <remarks>
/// The caller normalizes. The key identifier is part of the stored value so that, during a
/// blind-index rotation, rows hashed under either key are still found by
/// <see cref="ComputeAll"/>.
/// </remarks>
public sealed class HmacBlindIndex(Func<FieldKeySet> keys) : IBlindIndex
{
    private readonly Lazy<FieldKeySet> _keys = new(keys, LazyThreadSafetyMode.ExecutionAndPublication);

    public string Compute(string normalizedValue)
    {
        var ring = _keys.Value.BlindIndex;
        return Hash(ring.ActiveId, ring.ActiveKey, normalizedValue);
    }

    public IReadOnlyList<string> ComputeAll(string normalizedValue) =>
        [.. _keys.Value.BlindIndex.Keys.Select(pair => Hash(pair.Key, pair.Value, normalizedValue))];

    private static string Hash(string keyId, byte[] key, string value) =>
        $"{keyId}.{Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)))}";
}
