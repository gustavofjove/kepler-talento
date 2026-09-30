using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeplerTalento.Application.Abstractions.Encryption;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// Encrypts the free-text member of a stored search filter document (KTL-33 design decision 7).
/// </summary>
/// <remarks>
/// Only <c>text</c> is personal — a saved search term can be a candidate's name — so only it is
/// encrypted, and the document stays a JSON object with its <c>version</c>: the column's shape
/// checks keep working and <c>SearchFilterDocument</c> never sees an envelope. A document without
/// a text term passes through byte for byte.
/// </remarks>
public sealed class EncryptedFilterDocumentConverter(IFieldProtector protector, FieldContext context)
    : ValueConverter<string, string>(
        document => Transform(document, value => protector.Protect(value, context), encrypted: false),
        document => Transform(document, value => protector.Unprotect(value, context), encrypted: true))
{
    public const string TextMember = "text";

    private static readonly JsonSerializerOptions Output = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Transform(string document, Func<string, string> transform, bool encrypted)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(document);
        }
        catch (JsonException)
        {
            // Not this converter's decision: SearchFilterDocument refuses an unreadable document.
            return document;
        }
        if (root is not JsonObject json
            || json[TextMember] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || text.Length == 0
            // On write every term is encrypted, even one that happens to look like an envelope.
            || (encrypted && !AesGcmFieldProtector.IsEnvelope(text)))
        {
            return document;
        }
        json[TextMember] = transform(text);
        return json.ToJsonString(Output);
    }
}
