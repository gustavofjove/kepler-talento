using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>Section <c>FieldEncryption</c>: where the API finds its key file (KTL-33).</summary>
public sealed class FieldEncryptionOptions
{
    public const string SectionName = "FieldEncryption";

    public const string DefaultKeyFile = "/run/secrets/ktl_field_keys";

    public string KeyFile { get; init; } = DefaultKeyFile;
}

/// <summary>
/// The key file could not be used. <see cref="Code"/> is stable and safe to log; the message
/// never carries key material or the file's location.
/// </summary>
public sealed class FieldKeyException(string code, string message) : Exception(message)
{
    public const string Missing = "encryption.key.missing";
    public const string Malformed = "encryption.key.malformed";
    public const string WrongLength = "encryption.key.length";
    public const string UnknownActive = "encryption.key.active_unknown";
    public const string SharedKey = "encryption.key.shared";

    public string Code { get; } = code;
}

/// <summary>One purpose's keys: every key still able to read, and the one new writes use.</summary>
public sealed class FieldKeyRing
{
    public FieldKeyRing(string activeId, IReadOnlyDictionary<string, byte[]> keys)
    {
        ActiveId = activeId;
        Keys = keys;
    }

    public string ActiveId { get; }

    public IReadOnlyDictionary<string, byte[]> Keys { get; }

    public byte[] ActiveKey => Keys[ActiveId];
}

/// <summary>
/// The parsed key file. Two purposes, never sharing a key: encrypting values, and hashing the
/// e-mail blind index.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "encryption": { "active": "e1", "keys": { "e1": "&lt;base64 32 bytes&gt;" } },
///   "blindIndex": { "active": "b1", "keys": { "b1": "&lt;base64 32 bytes&gt;" } }
/// }
/// </code>
/// Keys are generated random bytes (<c>ktl-migrate encryption generate-keys</c>), never
/// derived from a password.
/// </remarks>
public sealed partial class FieldKeySet
{
    public const int KeyBytes = 32;

    private FieldKeySet(FieldKeyRing encryption, FieldKeyRing blindIndex)
    {
        Encryption = encryption;
        BlindIndex = blindIndex;
    }

    public FieldKeyRing Encryption { get; }

    public FieldKeyRing BlindIndex { get; }

    public static FieldKeySet Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FieldKeyException(FieldKeyException.Missing, "The field encryption key file is missing.");
        }
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FieldKeyException(FieldKeyException.Missing, "The field encryption key file cannot be read.");
        }
        return Parse(json);
    }

    public static FieldKeySet Parse(string json)
    {
        KeyFileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<KeyFileDocument>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            document = null;
        }
        if (document?.Encryption is null || document.BlindIndex is null)
        {
            throw new FieldKeyException(FieldKeyException.Malformed, "The field encryption key file is malformed.");
        }

        var encryption = ToRing(document.Encryption, "encryption");
        var blindIndex = ToRing(document.BlindIndex, "blindIndex");
        var allKeys = encryption.Keys.Values.Concat(blindIndex.Keys.Values).ToList();
        for (var left = 0; left < allKeys.Count; left++)
        {
            for (var right = left + 1; right < allKeys.Count; right++)
            {
                if (CryptographicOperations.FixedTimeEquals(allKeys[left], allKeys[right]))
                {
                    throw new FieldKeyException(FieldKeyException.SharedKey, "Two field encryption keys are identical.");
                }
            }
        }
        return new FieldKeySet(encryption, blindIndex);
    }

    public static string Serialize(FieldKeyRing encryption, FieldKeyRing blindIndex) =>
        JsonSerializer.Serialize(
            new KeyFileDocument(ToDocument(encryption), ToDocument(blindIndex)),
            new JsonSerializerOptions(SerializerOptions) { WriteIndented = true });

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeyBytes);

    public static bool IsValidKeyId(string id) => KeyIdPattern().IsMatch(id);

    private static FieldKeyRing ToRing(RingDocument ring, string purpose)
    {
        if (string.IsNullOrEmpty(ring.Active) || ring.Keys is null || ring.Keys.Count == 0)
        {
            throw new FieldKeyException(FieldKeyException.Malformed, $"The {purpose} keys are malformed.");
        }
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (id, encoded) in ring.Keys)
        {
            if (!IsValidKeyId(id))
            {
                throw new FieldKeyException(FieldKeyException.Malformed, $"A {purpose} key identifier is malformed.");
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encoded ?? string.Empty);
            }
            catch (FormatException)
            {
                throw new FieldKeyException(FieldKeyException.Malformed, $"A {purpose} key is not base64.");
            }
            if (bytes.Length != KeyBytes)
            {
                throw new FieldKeyException(FieldKeyException.WrongLength, $"A {purpose} key is not {KeyBytes * 8} bits.");
            }
            keys[id] = bytes;
        }
        if (!keys.ContainsKey(ring.Active))
        {
            throw new FieldKeyException(FieldKeyException.UnknownActive, $"The active {purpose} key is not in the key file.");
        }
        return new FieldKeyRing(ring.Active, keys);
    }

    private static RingDocument ToDocument(FieldKeyRing ring) => new(
        ring.ActiveId,
        ring.Keys.ToDictionary(pair => pair.Key, pair => (string?)Convert.ToBase64String(pair.Value), StringComparer.Ordinal));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    // Letters, digits, '_' and '-': the identifier sits between the dots of an envelope.
    [GeneratedRegex("^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex KeyIdPattern();

    private sealed record KeyFileDocument(RingDocument? Encryption, RingDocument? BlindIndex);

    private sealed record RingDocument(string? Active, Dictionary<string, string?>? Keys);
}
