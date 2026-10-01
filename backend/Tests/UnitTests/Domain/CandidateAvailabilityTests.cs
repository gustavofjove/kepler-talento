using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

/// <summary>
/// The availability check's invariants (KTL-36), held by the aggregate as well as by the validator
/// and the database, so a writer that skips the validator still cannot store an inconsistent check.
/// </summary>
public sealed class CandidateAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Checker = Guid.Parse("01932f00-0000-7000-8000-00000000c001");
    private static readonly DateOnly CheckedOn = new(2026, 3, 12);

    [Fact]
    public void A_new_candidate_is_unknown_with_no_dates_or_checker()
    {
        var candidate = New();

        Assert.Equal(CandidateAvailabilityStates.Unknown, candidate.AvailabilityState);
        Assert.Null(candidate.AvailabilityCheckedOn);
        Assert.Null(candidate.AvailabilityUntil);
        Assert.Null(candidate.AvailabilityCheckedByUserId);
    }

    [Fact]
    public void An_unavailable_check_stores_its_dates_and_checker_and_advances_the_update_time()
    {
        var candidate = New();
        var until = new DateOnly(2027, 1, 15);

        candidate.RecordAvailability(CandidateAvailabilityStates.Unavailable, CheckedOn, until, Checker, Now.AddHours(1));

        Assert.Equal(CandidateAvailabilityStates.Unavailable, candidate.AvailabilityState);
        Assert.Equal(CheckedOn, candidate.AvailabilityCheckedOn);
        Assert.Equal(until, candidate.AvailabilityUntil);
        Assert.Equal(Checker, candidate.AvailabilityCheckedByUserId);
        Assert.Equal(Now.AddHours(1), candidate.UpdatedAtUtc);
    }

    [Fact]
    public void An_until_date_on_the_check_date_is_accepted()
    {
        var candidate = New();

        candidate.RecordAvailability(CandidateAvailabilityStates.Unavailable, CheckedOn, CheckedOn, Checker, Now);

        Assert.Equal(CheckedOn, candidate.AvailabilityUntil);
    }

    [Fact]
    public void A_known_check_without_a_stored_user_has_no_checker()
    {
        var candidate = New();

        candidate.RecordAvailability(CandidateAvailabilityStates.Available, CheckedOn, null, null, Now);

        Assert.Equal(CandidateAvailabilityStates.Available, candidate.AvailabilityState);
        Assert.Null(candidate.AvailabilityCheckedByUserId);
    }

    [Fact]
    public void Recording_unknown_clears_the_dates_and_the_checker()
    {
        var candidate = New();
        candidate.RecordAvailability(CandidateAvailabilityStates.Unavailable, CheckedOn, new DateOnly(2027, 1, 15), Checker, Now);

        candidate.RecordAvailability(CandidateAvailabilityStates.Unknown, null, null, Checker, Now);

        Assert.Equal(CandidateAvailabilityStates.Unknown, candidate.AvailabilityState);
        Assert.Null(candidate.AvailabilityCheckedOn);
        Assert.Null(candidate.AvailabilityUntil);
        Assert.Null(candidate.AvailabilityCheckedByUserId);
    }

    [Fact]
    public void A_value_outside_the_set_is_refused_and_nothing_changes()
    {
        var candidate = New();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            candidate.RecordAvailability("in_process", CheckedOn, null, Checker, Now));
        Assert.Equal(CandidateAvailabilityStates.Unknown, candidate.AvailabilityState);
    }

    public static TheoryData<string, DateOnly?, DateOnly?> Inconsistent => new()
    {
        // A known check needs its date.
        { CandidateAvailabilityStates.Available, null, null },
        { CandidateAvailabilityStates.Unavailable, null, new DateOnly(2027, 1, 15) },
        // Unknown carries no dates.
        { CandidateAvailabilityStates.Unknown, CheckedOn, null },
        { CandidateAvailabilityStates.Unknown, null, new DateOnly(2027, 1, 15) },
        // An until date only on an unavailable check, never before the check date.
        { CandidateAvailabilityStates.Available, CheckedOn, new DateOnly(2027, 1, 15) },
        { CandidateAvailabilityStates.Unavailable, CheckedOn, CheckedOn.AddDays(-1) },
    };

    [Theory]
    [MemberData(nameof(Inconsistent))]
    public void An_inconsistent_check_is_refused_and_the_stored_one_is_kept(string state, DateOnly? checkedOn, DateOnly? until)
    {
        var candidate = New();
        candidate.RecordAvailability(CandidateAvailabilityStates.Available, CheckedOn, null, Checker, Now);

        Assert.Throws<ArgumentException>(() => candidate.RecordAvailability(state, checkedOn, until, Checker, Now));

        Assert.Equal(CandidateAvailabilityStates.Available, candidate.AvailabilityState);
        Assert.Equal(CheckedOn, candidate.AvailabilityCheckedOn);
    }

    [Fact]
    public void The_audit_catalogue_lists_the_check_and_still_reads_the_former_status_change()
    {
        Assert.Equal("candidate.availability_checked", CandidateAuditEvents.AvailabilityChecked);
        Assert.True(AuditEventTypes.IsKnown(CandidateAuditEvents.AvailabilityChecked));
        // No longer written, but kept so events recorded before KTL-36 still read and filter.
        Assert.True(AuditEventTypes.IsKnown(CandidateAuditEvents.StatusChanged));
    }

    private static Candidate New() => new(Guid.CreateVersion7(), "Ana", "Ruiz", Now);
}
