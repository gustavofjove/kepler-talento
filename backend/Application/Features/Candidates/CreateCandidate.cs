using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record CreateCandidateCommand(
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    string Location,
    string Province,
    string Country,
    string Availability,
    string Status,
    string Source,
    string Notes,
    string? ReceivedAt,
    string? ConsentAt,
    string? ReviewDueAt) : IRequest<CandidateResponse>;

public sealed class CreateCandidateValidator : AbstractValidator<CreateCandidateCommand>
{
    public CreateCandidateValidator()
    {
        RuleFor(command => command.FirstName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(CandidateErrors.FirstNameRequired)
            .WithMessage(CandidateErrors.FirstNameRequiredMessage);
        RuleFor(command => command.LastName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(CandidateErrors.LastNameRequired)
            .WithMessage(CandidateErrors.LastNameRequiredMessage);
        RuleFor(command => command.Status).MustBeAPermittedStatus();
        RuleFor(command => command.ReceivedAt).MustBeAWireDate();
        RuleFor(command => command.ConsentAt).MustBeAWireDate();
        RuleFor(command => command.ReviewDueAt).MustBeAWireDate();
    }
}

public sealed class CreateCandidateHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    ICurrentActor actor)
    : IRequestHandler<CreateCandidateCommand, CandidateResponse>
{
    public async Task<CandidateResponse> Handle(
        CreateCandidateCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireCreate(actor);
        var now = DateTimeOffset.UtcNow;
        var candidate = new Candidate(
            Guid.CreateVersion7(),
            request.FirstName.Trim(),
            request.LastName.Trim(),
            now);
        candidate.SetDetails(
            request.Phone,
            request.Email,
            request.Location,
            request.Province,
            request.Country,
            request.Availability,
            request.Status,
            request.Source,
            request.Notes,
            now);
        // Stored exactly as supplied. An absent consent date stays absent; nothing here
        // substitutes today, and nothing derives a review date from a received one.
        CandidateDates.TryFromWire(request.ReceivedAt, out var receivedAt);
        CandidateDates.TryFromWire(request.ConsentAt, out var consentAt);
        CandidateDates.TryFromWire(request.ReviewDueAt, out var reviewDueAt);
        candidate.SetConsent(receivedAt, consentAt, reviewDueAt, now);

        candidates.Add(candidate);
        var outcome = await candidates.SaveAsync(
            CandidateAuditEvents.Created,
            candidate.Id.ToString("N"),
            cancellationToken);
        if (outcome != CandidateSaveOutcome.Saved)
        {
            throw CandidateGuards.ToException(outcome);
        }
        return await CandidateProjection.ToResponseAsync(
            candidate,
            [],
            new CandidateCatalogLookup(catalogs),
            cancellationToken);
    }
}
