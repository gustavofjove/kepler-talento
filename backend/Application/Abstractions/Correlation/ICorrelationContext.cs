namespace KeplerTalento.Application.Abstractions.Correlation;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
