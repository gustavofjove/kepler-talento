using System.Text.Json;
using KeplerTalento.Application.Abstractions.Encryption;
using KeplerTalento.Application.Import;
using KeplerTalento.Infrastructure.Encryption;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Encryption;

/// <summary>
/// KTL-33 field encryption: the envelope, the key file and the blind index.
/// </summary>
public sealed class FieldEncryptionTests
{
    private static readonly FieldContext FirstName = FieldContext.For("CND_Candidates", "FirstName");
    private static readonly FieldContext LastName = FieldContext.For("CND_Candidates", "LastName");

    [Fact]
    public void A_value_round_trips_through_its_own_column()
    {
        var protector = Protector(KeySet());

        var envelope = protector.Protect("Ángel Muñoz", FirstName);

        Assert.StartsWith("ktl1.e1.", envelope, StringComparison.Ordinal);
        Assert.DoesNotContain("Ángel", envelope, StringComparison.Ordinal);
        Assert.Equal("Ángel Muñoz", protector.Unprotect(envelope, FirstName));
    }

    [Fact]
    public void An_empty_value_is_encrypted_too()
    {
        var protector = Protector(KeySet());

        var envelope = protector.Protect(string.Empty, FirstName);

        Assert.True(AesGcmFieldProtector.IsEnvelope(envelope));
        Assert.Equal(string.Empty, protector.Unprotect(envelope, FirstName));
    }

    [Fact]
    public void The_same_value_never_produces_the_same_envelope()
    {
        var protector = Protector(KeySet());

        Assert.NotEqual(protector.Protect("ana@example.test", FirstName), protector.Protect("ana@example.test", FirstName));
    }

    [Fact]
    public void A_value_moved_to_another_column_does_not_decrypt()
    {
        var protector = Protector(KeySet());
        var envelope = protector.Protect("Ana", FirstName);

        var failure = Assert.Throws<FieldDecryptionException>(() => protector.Unprotect(envelope, LastName));

        Assert.Equal("authentication failed", failure.Reason);
        Assert.DoesNotContain("Ana", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tampered_value_does_not_decrypt()
    {
        var protector = Protector(KeySet());
        var envelope = protector.Protect("Ana", FirstName);
        var tampered = envelope[..^2] + (envelope[^2] == 'A' ? 'B' : 'A') + envelope[^1];

        Assert.Throws<FieldDecryptionException>(() => protector.Unprotect(tampered, FirstName));
    }

    [Theory]
    [InlineData("Ana")]
    [InlineData("ktl1.")]
    [InlineData("ktl1.e1.")]
    [InlineData("ktl1..AAAA")]
    [InlineData("ktl1.e1.!!!")]
    [InlineData("ktl1.e1.AAAA")]
    public void Something_that_is_not_an_envelope_does_not_decrypt(string value)
    {
        var protector = Protector(KeySet());

        Assert.Throws<FieldDecryptionException>(() => protector.Unprotect(value, FirstName));
    }

    [Fact]
    public void A_value_under_a_key_no_longer_in_the_file_does_not_decrypt()
    {
        var envelope = Protector(KeySet(activeEncryption: "old")).Protect("Ana", FirstName);

        var failure = Assert.Throws<FieldDecryptionException>(() => Protector(KeySet()).Unprotect(envelope, FirstName));

        Assert.Equal("unknown key", failure.Reason);
    }

    [Fact]
    public void After_rotation_old_values_still_decrypt_and_new_values_use_the_new_key()
    {
        var oldKey = FieldKeySet.NewKey();
        var before = Protector(KeySet(encryption: new() { ["e1"] = oldKey }, activeEncryption: "e1"));
        var oldEnvelope = before.Protect("Ana", FirstName);

        var after = Protector(KeySet(encryption: new() { ["e1"] = oldKey, ["e2"] = FieldKeySet.NewKey() }, activeEncryption: "e2"));

        Assert.Equal("Ana", after.Unprotect(oldEnvelope, FirstName));
        Assert.False(after.IsProtectedWithActiveKey(oldEnvelope));
        Assert.StartsWith("ktl1.e2.", after.Protect("Ana", FirstName), StringComparison.Ordinal);
        Assert.True(after.IsProtectedWithActiveKey(after.Protect("Ana", FirstName)));
    }

    [Fact]
    public void Keys_are_read_on_first_use_not_on_construction()
    {
        var protector = new AesGcmFieldProtector(() => throw new FieldKeyException(FieldKeyException.Missing, "missing"));

        var failure = Assert.Throws<FieldKeyException>(() => protector.Protect("Ana", FirstName));

        Assert.Equal(FieldKeyException.Missing, failure.Code);
    }

    [Theory]
    [InlineData("not json", FieldKeyException.Malformed)]
    [InlineData("{}", FieldKeyException.Malformed)]
    [InlineData("""{"encryption":{"active":"e1","keys":{"e1":"AAAA"}},"blindIndex":{"active":"b1","keys":{"b1":"AAAA"}}}""", FieldKeyException.WrongLength)]
    [InlineData("""{"encryption":{"active":"e1","keys":{"e1":"***"}},"blindIndex":{"active":"b1","keys":{"b1":"AAAA"}}}""", FieldKeyException.Malformed)]
    [InlineData("""{"encryption":{"active":"e.1","keys":{"e.1":"AAAA"}},"blindIndex":{"active":"b1","keys":{"b1":"AAAA"}}}""", FieldKeyException.Malformed)]
    public void A_malformed_key_file_is_refused_with_a_stable_code(string json, string code)
    {
        var failure = Assert.Throws<FieldKeyException>(() => FieldKeySet.Parse(json));

        Assert.Equal(code, failure.Code);
    }

    [Fact]
    public void An_unknown_active_key_is_refused()
    {
        var json = Json(new() { ["e1"] = FieldKeySet.NewKey() }, "e9", new() { ["b1"] = FieldKeySet.NewKey() }, "b1");

        Assert.Equal(FieldKeyException.UnknownActive, Assert.Throws<FieldKeyException>(() => FieldKeySet.Parse(json)).Code);
    }

    [Fact]
    public void The_two_purposes_cannot_share_a_key()
    {
        var shared = FieldKeySet.NewKey();
        var json = Json(new() { ["e1"] = shared }, "e1", new() { ["b1"] = shared }, "b1");

        var failure = Assert.Throws<FieldKeyException>(() => FieldKeySet.Parse(json));

        Assert.Equal(FieldKeyException.SharedKey, failure.Code);
        Assert.DoesNotContain(Convert.ToBase64String(shared), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_key_file_is_refused_without_naming_its_location()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ktl-missing-{Guid.NewGuid():N}.json");

        var failure = Assert.Throws<FieldKeyException>(() => FieldKeySet.Load(path));

        Assert.Equal(FieldKeyException.Missing, failure.Code);
        Assert.DoesNotContain(path, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_serialized_key_file_parses_back_to_the_same_keys()
    {
        var keys = KeySet();

        var parsed = FieldKeySet.Parse(FieldKeySet.Serialize(keys.Encryption, keys.BlindIndex));

        Assert.Equal(keys.Encryption.ActiveKey, parsed.Encryption.ActiveKey);
        Assert.Equal(keys.BlindIndex.ActiveKey, parsed.BlindIndex.ActiveKey);
    }

    [Fact]
    public void The_blind_index_is_stable_keyed_and_never_the_value()
    {
        var keys = KeySet();
        var index = new HmacBlindIndex(() => keys);
        var normalized = CandidateImportRowEvaluator.NormalizeEmail("  Ana.Garcia@Example.TEST ");

        var hash = index.Compute(normalized);

        Assert.Equal(hash, index.Compute(CandidateImportRowEvaluator.NormalizeEmail("ana.garcia@example.test")));
        Assert.StartsWith("b1.", hash, StringComparison.Ordinal);
        Assert.Equal(3 + 64, hash.Length);
        Assert.DoesNotContain("ana", hash, StringComparison.Ordinal);
        Assert.NotEqual(hash, new HmacBlindIndex(KeySet).Compute(normalized));
    }

    [Fact]
    public void During_blind_index_rotation_lookups_cover_both_keys()
    {
        var oldKey = FieldKeySet.NewKey();
        var before = new HmacBlindIndex(() => KeySet(blindIndex: new() { ["b1"] = oldKey }, activeBlindIndex: "b1"));
        var during = new HmacBlindIndex(() => KeySet(blindIndex: new() { ["b1"] = oldKey, ["b2"] = FieldKeySet.NewKey() }, activeBlindIndex: "b2"));

        var stored = before.Compute("ana@example.test");

        Assert.Contains(stored, during.ComputeAll("ana@example.test"));
        Assert.StartsWith("b2.", during.Compute("ana@example.test"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_context_without_encryption_refuses_rather_than_passing_plaintext()
    {
        Assert.Throws<InvalidOperationException>(() => UnavailableFieldProtector.Instance.Protect("Ana", FirstName));
        Assert.Throws<InvalidOperationException>(() => UnavailableFieldProtector.Instance.Unprotect("Ana", FirstName));
    }

    [Fact]
    public void The_converter_refuses_a_value_beyond_the_column_limit()
    {
        var converter = new EncryptedStringConverter(Protector(KeySet()), FirstName, 5);
        var toProvider = converter.ConvertToProviderExpression.Compile();

        Assert.True(AesGcmFieldProtector.IsEnvelope(toProvider("Ana")));
        var failure = Assert.Throws<InvalidOperationException>(() => toProvider("Anastasia"));
        Assert.DoesNotContain("Anastasia", failure.Message, StringComparison.Ordinal);
    }

    private static AesGcmFieldProtector Protector(FieldKeySet keys) => new(() => keys);

    private static FieldKeySet KeySet() => KeySet(null, null, null, null);

    private static FieldKeySet KeySet(
        Dictionary<string, byte[]>? encryption = null,
        string? activeEncryption = null,
        Dictionary<string, byte[]>? blindIndex = null,
        string? activeBlindIndex = null)
    {
        activeEncryption ??= "e1";
        activeBlindIndex ??= "b1";
        encryption ??= new() { [activeEncryption] = FieldKeySet.NewKey() };
        blindIndex ??= new() { [activeBlindIndex] = FieldKeySet.NewKey() };
        return FieldKeySet.Parse(Json(encryption, activeEncryption, blindIndex, activeBlindIndex));
    }

    private static string Json(
        Dictionary<string, byte[]> encryption,
        string activeEncryption,
        Dictionary<string, byte[]> blindIndex,
        string activeBlindIndex) =>
        JsonSerializer.Serialize(new
        {
            encryption = new { active = activeEncryption, keys = encryption.ToDictionary(pair => pair.Key, pair => Convert.ToBase64String(pair.Value)) },
            blindIndex = new { active = activeBlindIndex, keys = blindIndex.ToDictionary(pair => pair.Key, pair => Convert.ToBase64String(pair.Value)) },
        });
}
