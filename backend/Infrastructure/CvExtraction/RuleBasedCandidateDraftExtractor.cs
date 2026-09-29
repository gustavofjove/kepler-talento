using System.Text.RegularExpressions;
using KeplerTalento.Application.Abstractions.CvExtraction;
using PhoneNumbers;

namespace KeplerTalento.Infrastructure.CvExtraction;

/// <summary>
/// Deterministic rules that turn CV text into suggestions (KTL-32 design D5). No I/O, no model,
/// no external service, so the rules run unchanged in unit tests and in the corpus harness.
/// </summary>
/// <remarks>
/// <para>
/// "Contact region": the first lines of page one, where CVs put name, e-mail, phone and address.
/// Locations are only looked for there, so a former employer's address in the experience section
/// is not suggested as where the candidate lives.
/// </para>
/// <para>
/// Confidence is <c>high</c> only when a second signal corroborates the value: the e-mail's local
/// part for a name, a postcode for a place. Everything else is <c>low</c>.
/// </para>
/// </remarks>
public sealed partial class RuleBasedCandidateDraftExtractor(SpanishPlaces places) : ICandidateDraftExtractor
{
    private const string DefaultRegion = "ES";
    private const int MinimumContactLines = 12;
    private const double ContactRegionShare = 0.3;
    private const int MaximumNameCandidates = 3;

    /// <summary>Words that make a line a heading, a job title or a label rather than a name.</summary>
    private static readonly HashSet<string> NotNameWords = new(StringComparer.Ordinal)
    {
        "curriculum", "vitae", "cv", "resume", "perfil", "profesional", "datos", "personales",
        "contacto", "experiencia", "laboral", "formacion", "academica", "educacion", "idiomas",
        "habilidades", "competencias", "conocimientos", "sobre", "mi", "about", "me", "profile",
        "summary", "resumen", "objetivo", "informacion", "personal", "email", "correo", "telefono",
        "movil", "direccion", "domicilio", "linkedin", "github", "portfolio", "espana", "spain",
        "desarrollador", "desarrolladora", "developer", "ingeniero", "ingeniera", "engineer",
        "analista", "consultor", "consultora", "tecnico", "tecnica", "manager", "director",
        "directora", "responsable", "junior", "senior", "full", "stack", "backend", "frontend",
        "software", "programador", "programadora", "administrativo", "administrativa", "jefe",
        "jefa", "gerente", "especialista", "licenciado", "licenciada", "grado", "master",
        "referencias", "certificaciones", "cursos", "proyectos", "skills", "experience",
        "education", "languages", "contact", "nacionalidad", "fecha", "nacimiento", "carnet",
    };

    /// <summary>Particles that may appear inside a Spanish name but never start one.</summary>
    private static readonly HashSet<string> NameParticles = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "los", "y", "i", "da", "das", "do", "dos", "van", "von",
    };

    private readonly PhoneNumberUtil _phones = PhoneNumberUtil.GetInstance();

    public CandidateDraftSuggestions Extract(CvText text)
    {
        if (!text.HasText)
        {
            return CandidateDraftSuggestions.None;
        }
        var lines = text.Lines;
        var contact = ContactRegion(lines);

        var email = FindEmail(lines);
        var phone = FindPhone(lines, contact.Count);
        var (firstName, lastName, nameLine) = FindName(contact, email?.Value);
        var (location, province) = FindPlace(contact, nameLine);

        return new CandidateDraftSuggestions(firstName, lastName, email, phone, location, province);
    }

    private static IReadOnlyList<CvLine> ContactRegion(IReadOnlyList<CvLine> lines)
    {
        var firstPage = lines.Where(line => line.Page == 0).ToList();
        var take = Math.Max(MinimumContactLines, (int)Math.Ceiling(firstPage.Count * ContactRegionShare));
        return firstPage.Take(take).ToList();
    }

    // ---- e-mail -------------------------------------------------------------------------------

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    private static FieldSuggestion? FindEmail(IReadOnlyList<CvLine> lines)
    {
        foreach (var line in lines)
        {
            var match = EmailPattern().Match(line.Text);
            if (match.Success)
            {
                return new(match.Value.Trim('.').ToLowerInvariant(), SuggestionConfidence.High);
            }
        }
        return null;
    }

    // ---- phone --------------------------------------------------------------------------------

    private FieldSuggestion? FindPhone(IReadOnlyList<CvLine> lines, int contactLines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            foreach (var match in _phones.FindNumbers(lines[index].Text, DefaultRegion, PhoneNumberUtil.Leniency.VALID, long.MaxValue))
            {
                var number = match.Number;
                var region = _phones.GetRegionCodeForNumber(number);
                var formatted = region == DefaultRegion
                    ? _phones.Format(number, PhoneNumberFormat.NATIONAL)
                    : _phones.Format(number, PhoneNumberFormat.INTERNATIONAL);
                var inContact = lines[index].Page == 0 && index < contactLines;
                return new(formatted, inContact ? SuggestionConfidence.High : SuggestionConfidence.Low);
            }
        }
        return null;
    }

    // ---- name ---------------------------------------------------------------------------------

    private (FieldSuggestion? First, FieldSuggestion? Last, CvLine? Line) FindName(
        IReadOnlyList<CvLine> contact,
        string? email)
    {
        var candidates = contact
            .Select((line, order) => (Line: line, Order: order, Words: NameWords(line.Text)))
            .Where(candidate => candidate.Words is not null)
            .ToList();
        if (candidates.Count == 0)
        {
            return (null, null, null);
        }

        // With point sizes, the largest text wins (ties to the earliest); without, the earliest.
        var chosen = candidates
            .Take(candidates.Any(candidate => candidate.Line.FontSize is not null) ? candidates.Count : MaximumNameCandidates)
            .OrderByDescending(candidate => candidate.Line.FontSize ?? 0)
            .ThenBy(candidate => candidate.Order)
            .First();
        var words = chosen.Words!;

        var localTokens = LocalPartTokens(email);
        var corroborated = words.Any(word => Corroborates(word, localTokens));
        var givenCount = GivenNameCount(words, localTokens);
        var confidence = corroborated ? SuggestionConfidence.High : SuggestionConfidence.Low;

        var first = string.Join(' ', words.Take(givenCount));
        var last = string.Join(' ', words.Skip(givenCount));
        return (
            new FieldSuggestion(first, confidence),
            last.Length > 0 ? new FieldSuggestion(last, confidence) : null,
            chosen.Line);
    }

    /// <summary>The line's words when it looks like a personal name, in display case; else null.</summary>
    private string[]? NameWords(string text)
    {
        if (text.Contains('@') || text.Any(char.IsDigit) || text.Contains(':') || text.Contains('/') || text.Contains(','))
        {
            return null;
        }
        var raw = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length is < 2 or > 6)
        {
            return null;
        }
        foreach (var word in raw)
        {
            if (!word.All(character => char.IsLetter(character) || character is '-' or '\'' or '’' or '.'))
            {
                return null;
            }
            var normalized = TextNormalization.Normalize(word);
            if (NotNameWords.Contains(normalized))
            {
                return null;
            }
        }
        if (NameParticles.Contains(TextNormalization.Normalize(raw[0])))
        {
            return null;
        }
        var significant = raw.Count(word => !NameParticles.Contains(TextNormalization.Normalize(word)));
        if (significant is < 2 or > 5)
        {
            return null;
        }
        // A line of places ("Madrid Barcelona") is not a name. One place among other words is
        // fine: León, Toledo and Zamora are surnames too.
        var whole = TextNormalization.Normalize(text);
        if (places.FindMunicipalities(whole).Count > 0
            || places.FindProvince(whole) is not null
            || raw.All(word => IsPlace(TextNormalization.Normalize(word))))
        {
            return null;
        }
        return raw.Select(DisplayCase).ToArray();
    }

    private bool IsPlace(string normalized) =>
        places.FindMunicipalities(normalized).Count > 0 || places.FindProvince(normalized) is not null;

    private static string DisplayCase(string word)
    {
        var normalized = TextNormalization.Normalize(word);
        if (NameParticles.Contains(normalized))
        {
            return word.ToLowerInvariant();
        }
        var isShouting = word.Any(char.IsLetter) && word.Where(char.IsLetter).All(char.IsUpper);
        if (!isShouting)
        {
            return word;
        }
        // "GARCÍA-LÓPEZ" → "García-López". Invariant casing on purpose: the API container runs in
        // globalization-invariant mode, where asking for "es-ES" throws. Invariant casing already
        // maps Á/á, Ñ/ñ, Ü/ü correctly.
        var characters = word.ToLowerInvariant().ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (index == 0 || characters[index - 1] is '-' or '\'' or '’')
            {
                characters[index] = char.ToUpperInvariant(characters[index]);
            }
        }
        return new string(characters);
    }

    private static string[] LocalPartTokens(string? email)
    {
        if (email is null)
        {
            return [];
        }
        var local = email[..email.IndexOf('@')];
        return Regex.Split(local.ToLowerInvariant(), @"[^a-z]+")
            .Where(token => token.Length > 0)
            .ToArray();
    }

    private static bool Corroborates(string word, string[] localTokens)
    {
        var normalized = TextNormalization.Normalize(word).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length < 3 || NameParticles.Contains(normalized))
        {
            return false;
        }
        return localTokens.Any(token => token == normalized || (normalized.Length >= 4 && token.Contains(normalized, StringComparison.Ordinal)));
    }

    /// <summary>
    /// How many leading words are the given name. The e-mail decides when its first token is
    /// several name words run together ("juancarlos.perez"); otherwise Spanish convention: two
    /// surnames, so one given name for three words and two for four.
    /// </summary>
    private static int GivenNameCount(string[] words, string[] localTokens)
    {
        var normalized = words.Select(word => TextNormalization.Normalize(word).Replace(" ", string.Empty, StringComparison.Ordinal)).ToArray();
        if (localTokens.Length > 0)
        {
            for (var count = Math.Min(3, words.Length - 1); count >= 2; count--)
            {
                if (localTokens[0] == string.Concat(normalized.Take(count)))
                {
                    return count;
                }
            }
            // "maria.jose.lopez": the local part spells out a two-word given name before a surname.
            if (localTokens.Length >= 3 && words.Length >= 4
                && localTokens[0] == normalized[0] && localTokens[1] == normalized[1])
            {
                return 2;
            }
        }
        var significant = words.Count(word => !NameParticles.Contains(TextNormalization.Normalize(word)));
        return significant >= 4 ? 2 : 1;
    }

    // ---- location and province ----------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d+])(?<postcode>(0[1-9]|[1-4]\d|5[0-2])\d{3})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PostcodePattern();

    [GeneratedRegex(@"\+?\d[\d\s().\-]{7,}\d", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneLikeRun();

    private (FieldSuggestion? Location, FieldSuggestion? Province) FindPlace(
        IReadOnlyList<CvLine> contact,
        CvLine? nameLine)
    {
        var lines = contact
            .Where(line => !ReferenceEquals(line, nameLine) && !line.Text.Contains('@'))
            .ToList();

        // 1. A postcode fixes the province; a municipality on or next to its line fixes the place.
        for (var index = 0; index < lines.Count; index++)
        {
            var withoutPhones = PhoneLikeRun().Replace(lines[index].Text, " ");
            var match = PostcodePattern().Match(withoutPhones);
            if (!match.Success)
            {
                continue;
            }
            var province = places.ProvinceOfPostcode(match.Groups["postcode"].Value);
            if (province is null)
            {
                continue;
            }
            var neighbours = new[] { index, index - 1, index + 1 }
                .Where(position => position >= 0 && position < lines.Count)
                .Select(position => lines[position].Text);
            foreach (var text in neighbours)
            {
                var municipality = Municipalities(text).FirstOrDefault(found => found.ProvinceCode == province.Code);
                if (municipality is not null)
                {
                    return (
                        new FieldSuggestion(municipality.Name, SuggestionConfidence.High),
                        new FieldSuggestion(province.Name, SuggestionConfidence.High));
                }
            }
            return (null, new FieldSuggestion(province.Name, SuggestionConfidence.High));
        }

        // 2. No postcode: an unambiguous municipality, else a province name. Uncorroborated.
        foreach (var line in lines)
        {
            var found = Municipalities(line.Text).ToList();
            var distinct = found.DistinctBy(municipality => municipality.Code).ToList();
            if (distinct.Count == 1)
            {
                var municipality = distinct[0];
                var province = places.ProvinceByCode(municipality.ProvinceCode);
                return (
                    new FieldSuggestion(municipality.Name, SuggestionConfidence.Low),
                    province is null ? null : new FieldSuggestion(province.Name, SuggestionConfidence.Low));
            }
        }
        foreach (var line in lines)
        {
            var province = Provinces(line.Text).FirstOrDefault();
            if (province is not null)
            {
                return (null, new FieldSuggestion(province.Name, SuggestionConfidence.Low));
            }
        }
        return (null, null);
    }

    /// <summary>Municipalities named in a line, longest spans first.</summary>
    private IEnumerable<SpanishMunicipality> Municipalities(string text)
    {
        foreach (var span in Spans(text))
        {
            foreach (var municipality in places.FindMunicipalities(span))
            {
                yield return municipality;
            }
        }
    }

    private IEnumerable<SpanishProvince> Provinces(string text)
    {
        foreach (var span in Spans(text))
        {
            if (places.FindProvince(span) is { } province)
            {
                yield return province;
            }
        }
    }

    private IEnumerable<string> Spans(string text)
    {
        var words = TextNormalization.Words(text);
        for (var length = Math.Min(places.MaximumWords, words.Length); length >= 1; length--)
        {
            for (var start = 0; start + length <= words.Length; start++)
            {
                yield return string.Join(' ', words, start, length);
            }
        }
    }
}
