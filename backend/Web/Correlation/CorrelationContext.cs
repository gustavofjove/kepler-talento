using KeplerTalento.Application.Abstractions.Correlation;

namespace KeplerTalento.Web.Correlation;

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; internal set; } = string.Empty;
}
