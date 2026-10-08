namespace KeplerTalento.Infrastructure.Documents;

public sealed class ClamAvOptions
{
    public const string SectionName = "ClamAv";
    public string Host { get; init; } = "clamav";
    public int Port { get; init; } = 3310;
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Marks every file clean without contacting ClamAV, for a non-production server that has no
    /// scanner yet. Start-up refuses it in Production. Files promoted this way record the
    /// <c>scanner.bypassed</c> outcome, so they can be found and rescanned later.
    /// </summary>
    public bool Bypass { get; init; }
}
