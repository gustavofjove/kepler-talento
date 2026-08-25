namespace KeplerTalento.Infrastructure.Documents;

public sealed class ClamAvOptions
{
    public const string SectionName = "ClamAv";
    public string Host { get; init; } = "clamav";
    public int Port { get; init; } = 3310;
    public int TimeoutSeconds { get; init; } = 60;
}
