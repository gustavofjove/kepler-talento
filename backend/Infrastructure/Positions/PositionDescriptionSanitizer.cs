using Ganss.Xss;
using KeplerTalento.Application.Abstractions.Positions;

namespace KeplerTalento.Infrastructure.Positions;

public sealed class PositionDescriptionSanitizer : IPositionDescriptionSanitizer
{
    private static readonly string[] ParserTags =
        ["p", "br", "strong", "em", "ul", "ol", "li", "span", "b", "i"];

    public string Sanitize(string html)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        foreach (var tag in ParserTags) sanitizer.AllowedTags.Add(tag);
        // Lexical wraps text in styled spans and exports emphasis using b/i. Attributes and
        // CSS are removed above; canonicalize those inert editor wrappers to the API allowlist.
        return sanitizer.Sanitize(html)
            .Replace("<span>", string.Empty, StringComparison.Ordinal)
            .Replace("</span>", string.Empty, StringComparison.Ordinal)
            .Replace("<b>", "<strong>", StringComparison.Ordinal)
            .Replace("</b>", "</strong>", StringComparison.Ordinal)
            .Replace("<i>", "<em>", StringComparison.Ordinal)
            .Replace("</i>", "</em>", StringComparison.Ordinal)
            .Trim();
    }
}
