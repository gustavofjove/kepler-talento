using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// The searchable columns of <c>CND_Candidates</c> exactly as stored: envelopes, not plaintext
/// (KTL-33 design decision 6).
/// </summary>
/// <remarks>
/// Read-only and keyless, mapped as a view over the candidate table so EF neither migrates nor
/// writes it. It exists for one reason: the encrypted search stage decrypts tens of thousands of
/// values, and doing that in parallel and stopping at the first matching field is several times
/// faster than letting the entity's converters decrypt every field of every row on one thread.
/// Nothing else may use it; plaintext is always read through <c>Candidate</c>.
/// </remarks>
public sealed class CandidateCiphertext
{
    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string Notes { get; private set; } = string.Empty;

    // Clear columns the in-API order needs.
    public DateOnly? AvailabilityCheckedOn { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}

internal sealed class CandidateCiphertextConfiguration : IEntityTypeConfiguration<CandidateCiphertext>
{
    public void Configure(EntityTypeBuilder<CandidateCiphertext> builder)
    {
        builder.HasNoKey();
        builder.ToView(CandidateConfiguration.Table);
    }
}
