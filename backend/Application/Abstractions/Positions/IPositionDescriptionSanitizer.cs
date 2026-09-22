namespace KeplerTalento.Application.Abstractions.Positions;

public interface IPositionDescriptionSanitizer
{
    string Sanitize(string html);
}
