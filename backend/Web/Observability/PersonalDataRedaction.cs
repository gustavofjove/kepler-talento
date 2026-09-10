using Serilog.Core;
using Serilog.Events;

namespace KeplerTalento.Web.Observability;

/// <summary>
/// Masks candidate personal data in log events, whatever put it there.
/// </summary>
/// <remarks>
/// This exists instead of relying on slices not to log these fields. "No slice logs a
/// candidate name" is true until someone adds a slice, adds a diagnostic, or a library
/// destructures a request object into its properties — and the failure is silent and
/// permanent, because logs are shipped and retained. An enricher stays true as the
/// application grows, which is the only form of this guarantee worth having.
///
/// It masks by property name, at every depth, so a candidate field caught inside a
/// destructured request or an exception's structured data is masked as readily as one
/// logged directly.
/// </remarks>
public sealed class PersonalDataRedactionEnricher : ILogEventEnricher
{
    public const string Mask = "[redacted]";

    /// <summary>
    /// The candidate fields that are personal data. Consent and retention dates are in
    /// the set because when someone consented, and when their record is due for review,
    /// say something about that person too.
    /// </summary>
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "firstName",
        "lastName",
        "phone",
        "email",
        "location",
        "province",
        "notes",
        "receivedAt",
        "consentAt",
        "reviewDueAt",
        "originalFilename",
        "originalFileName",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            var redacted = Redact(property.Key, property.Value);
            if (!ReferenceEquals(redacted, property.Value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, redacted));
            }
        }
    }

    private static LogEventPropertyValue Redact(string name, LogEventPropertyValue value)
    {
        if (ProtectedNames.Contains(name))
        {
            return new ScalarValue(Mask);
        }
        return value switch
        {
            StructureValue structure => RedactStructure(structure),
            SequenceValue sequence => RedactSequence(sequence),
            DictionaryValue dictionary => RedactDictionary(dictionary),
            _ => value,
        };
    }

    private static LogEventPropertyValue RedactStructure(StructureValue structure)
    {
        var properties = structure.Properties
            .Select(property => new LogEventProperty(property.Name, Redact(property.Name, property.Value)))
            .ToList();
        return new StructureValue(properties, structure.TypeTag);
    }

    private static LogEventPropertyValue RedactSequence(SequenceValue sequence) =>
        new SequenceValue(sequence.Elements.Select(element => Redact(string.Empty, element)).ToList());

    private static LogEventPropertyValue RedactDictionary(DictionaryValue dictionary) =>
        new DictionaryValue(dictionary.Elements.Select(element => KeyValuePair.Create(
            element.Key,
            Redact(element.Key.Value as string ?? string.Empty, element.Value))));
}
