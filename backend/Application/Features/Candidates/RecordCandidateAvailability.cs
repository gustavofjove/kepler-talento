using System.Globalization;
using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

/// <summary>
/// Records the candidate's availability check (KTL-36). Every availability write is a check:
/// reconfirming, changing, resetting to unknown and undoing all arrive here, so there is one
/// rule to test and none to bypass. The checker is the current actor, never the caller's input.
/// </summary>
public sealed record RecordCandidateAvailabilityCommand(
    Guid Id,
    string? State,
    string? CheckedOn,
    string? Until,
    uint Version) : IRequest<CandidateResponse>;

public sealed class RecordCandidateAvailabilityValidator : AbstractValidator<RecordCandidateAvailabilityCommand>
{
    public RecordCandidateAvailabilityValidator()
    {
        RuleFor(command => command.State)
            .Must(CandidateAvailabilityStates.IsKnown)
            .WithErrorCode(CandidateErrors.AvailabilityInvalid)
            .WithMessage(CandidateErrors.AvailabilityInvalidMessage);

        When(command => command.State == CandidateAvailabilityStates.Unknown, () =>
        {
            // Unknown carries no dates: the reset clears them rather than keeping stale ones.
            RuleFor(command => command.CheckedOn)
                .Must(string.IsNullOrWhiteSpace)
                .WithErrorCode(CandidateErrors.AvailabilityCheckedOnInvalid)
                .WithMessage(CandidateErrors.AvailabilityCheckedOnInvalidMessage);
            RuleFor(command => command.Until)
                .Must(string.IsNullOrWhiteSpace)
                .WithErrorCode(CandidateErrors.AvailabilityUntilInvalid)
                .WithMessage(CandidateErrors.AvailabilityUntilInvalidMessage);
        });

        When(command => command.State is CandidateAvailabilityStates.Available or CandidateAvailabilityStates.Unavailable, () =>
        {
            // An earlier date than the stored one is allowed on purpose: it is how a mistaken
            // check is amended. Only a date beyond tomorrow (UTC) is refused, which tolerates
            // users ahead of UTC without a configured office time zone.
            RuleFor(command => command.CheckedOn)
                .Must(value => AvailabilityDates.TryParse(value, out var day)
                    && day is not null
                    && day <= AvailabilityDates.LatestCheckDate(DateTimeOffset.UtcNow))
                .WithErrorCode(CandidateErrors.AvailabilityCheckedOnInvalid)
                .WithMessage(CandidateErrors.AvailabilityCheckedOnInvalidMessage);
            RuleFor(command => command.Until)
                .Must((command, value) => UntilIsValid(command.State!, command.CheckedOn, value))
                .WithErrorCode(CandidateErrors.AvailabilityUntilInvalid)
                .WithMessage(CandidateErrors.AvailabilityUntilInvalidMessage);
        });
    }

    private static bool UntilIsValid(string state, string? checkedOn, string? until)
    {
        if (string.IsNullOrWhiteSpace(until))
        {
            return true;
        }
        if (state != CandidateAvailabilityStates.Unavailable || !AvailabilityDates.TryParse(until, out var untilDay))
        {
            return false;
        }
        // An unparsable check date is the check-date rule's refusal, not this one's.
        return !AvailabilityDates.TryParse(checkedOn, out var checkedDay)
            || checkedDay is null
            || untilDay >= checkedDay;
    }
}

public sealed class RecordCandidateAvailabilityHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    ICurrentActor actor)
    : IRequestHandler<RecordCandidateAvailabilityCommand, CandidateResponse>
{
    public async Task<CandidateResponse> Handle(
        RecordCandidateAvailabilityCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireUpdate(actor);
        var candidate = await candidates.FindCoreAsync(request.Id, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (!candidate.IsActive)
        {
            throw CandidateGuards.Removed();
        }

        candidates.ExpectVersion(candidate, request.Version);
        AvailabilityDates.TryParse(request.CheckedOn, out var checkedOn);
        AvailabilityDates.TryParse(request.Until, out var until);
        candidate.RecordAvailability(
            request.State!,
            checkedOn,
            until,
            request.State == CandidateAvailabilityStates.Unknown ? null : actor.UserId,
            DateTimeOffset.UtcNow);

        var outcome = await candidates.SaveAsync(
            CandidateAuditEvents.AvailabilityChecked,
            candidate.Id.ToString("N"),
            cancellationToken);
        if (outcome != CandidateSaveOutcome.Saved)
        {
            throw CandidateGuards.ToException(outcome);
        }
        // Reloaded rather than projected from the tracked entity, so the checker's display name
        // is read for the user the check now names.
        return await CandidateProjection.ReloadAsync(
            candidates,
            new CandidateCatalogLookup(catalogs),
            candidate.Id,
            cancellationToken);
    }
}

/// <summary>The availability check's wire dates: strictly <c>yyyy-MM-dd</c>, blank meaning none.</summary>
public static class AvailabilityDates
{
    /// <summary>The latest check date accepted at <paramref name="now"/>: the UTC date plus one day.</summary>
    public static DateOnly LatestCheckDate(DateTimeOffset now) =>
        DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);

    public static bool TryParse(string? value, out DateOnly? day)
    {
        day = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            day = parsed;
            return true;
        }
        return false;
    }
}
