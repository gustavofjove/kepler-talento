using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates.Notes;

public sealed record UpdateCandidateNoteCommand(Guid CandidateId, Guid NoteId, string? Body, uint Version)
    : IRequest<CandidateNoteResponse>;

public sealed class UpdateCandidateNoteValidator : AbstractValidator<UpdateCandidateNoteCommand>
{
    public UpdateCandidateNoteValidator()
    {
        RuleFor(command => command.Body)
            .NotEmpty()
            .WithErrorCode(CandidateErrors.NoteBodyRequired)
            .WithMessage("La nota no puede estar vacía.")
            .MaximumLength(CandidateNote.MaximumBodyLength)
            .WithErrorCode(CandidateErrors.NoteBodyTooLong)
            .WithMessage("La nota no puede superar los 4000 caracteres.");
        RuleFor(command => command.Version).GreaterThan(0u);
    }
}

public sealed class UpdateCandidateNoteHandler(ICandidateRepository candidates, ICurrentActor actor)
    : IRequestHandler<UpdateCandidateNoteCommand, CandidateNoteResponse>
{
    public async Task<CandidateNoteResponse> Handle(
        UpdateCandidateNoteCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireUpdate(actor);
        var candidate = await candidates.FindCoreAsync(request.CandidateId, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (!candidate.IsActive)
        {
            throw CandidateGuards.Removed();
        }
        var note = await candidates.FindNoteAsync(candidate.Id, request.NoteId, false, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        candidates.ExpectVersion(note, request.Version);
        note.Edit(request.Body!, DateTimeOffset.UtcNow);
        var outcome = await candidates.SaveNoteAsync(
            CandidateAuditEvents.NoteUpdated,
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
