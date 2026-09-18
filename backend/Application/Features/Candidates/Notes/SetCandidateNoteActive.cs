using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates.Notes;

public sealed record SetCandidateNoteActiveCommand(
    Guid CandidateId,
    Guid NoteId,
    bool IsActive,
    uint Version) : IRequest<CandidateNoteResponse>;

public sealed class SetCandidateNoteActiveValidator : AbstractValidator<SetCandidateNoteActiveCommand>
{
    public SetCandidateNoteActiveValidator() =>
        RuleFor(command => command.Version).GreaterThan(0u);
}

public sealed class SetCandidateNoteActiveHandler(ICandidateRepository candidates, ICurrentActor actor)
    : IRequestHandler<SetCandidateNoteActiveCommand, CandidateNoteResponse>
{
    public async Task<CandidateNoteResponse> Handle(
        SetCandidateNoteActiveCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireUpdate(actor);
        var candidate = await candidates.FindCoreAsync(request.CandidateId, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (!candidate.IsActive)
        {
            throw CandidateGuards.Removed();
        }
        var note = await candidates.FindNoteAsync(candidate.Id, request.NoteId, true, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (note.IsActive == request.IsActive)
        {
            return CandidateNoteResponse.From(note);
        }
        candidates.ExpectVersion(note, request.Version);
        note.SetActive(request.IsActive, DateTimeOffset.UtcNow);
        var outcome = await candidates.SaveNoteAsync(
            CandidateAuditEvents.NoteRetired,
            candidate.Id,
            note.Id,
            cancellationToken);
        if (outcome == CandidateSaveOutcome.ConcurrencyConflict)
        {
            throw CandidateGuards.NoteConflict();
        }
        if (outcome != CandidateSaveOutcome.Saved)
        {
            throw CandidateGuards.ToException(outcome);
        }
        return CandidateNoteResponse.From(note);
    }
}
