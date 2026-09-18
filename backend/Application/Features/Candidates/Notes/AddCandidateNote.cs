using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates.Notes;

public sealed record AddCandidateNoteCommand(Guid CandidateId, string? Body)
    : IRequest<CandidateNoteResponse>;

public sealed class AddCandidateNoteValidator : AbstractValidator<AddCandidateNoteCommand>
{
    public AddCandidateNoteValidator()
    {
        RuleFor(command => command.Body)
            .NotEmpty()
            .WithErrorCode(CandidateErrors.NoteBodyRequired)
            .WithMessage("La nota no puede estar vacía.")
            .MaximumLength(CandidateNote.MaximumBodyLength)
            .WithErrorCode(CandidateErrors.NoteBodyTooLong)
            .WithMessage("La nota no puede superar los 4000 caracteres.");
    }
}

public sealed class AddCandidateNoteHandler(ICandidateRepository candidates, ICurrentActor actor)
    : IRequestHandler<AddCandidateNoteCommand, CandidateNoteResponse>
{
    public async Task<CandidateNoteResponse> Handle(
        AddCandidateNoteCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireUpdate(actor);
        var candidate = await candidates.FindCoreAsync(request.CandidateId, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (!candidate.IsActive)
        {
            throw CandidateGuards.Removed();
        }

        var note = new CandidateNote(
            Guid.CreateVersion7(),
            candidate.Id,
            request.Body!,
            actor.UserId,
            DateTimeOffset.UtcNow);
        candidates.AddNote(note);
        var outcome = await candidates.SaveNoteAsync(
            CandidateAuditEvents.NoteAdded,
            candidate.Id,
            note.Id,
            cancellationToken);
        if (outcome != CandidateSaveOutcome.Saved)
        {
            throw CandidateGuards.ToException(outcome);
        }
        var stored = await candidates.FindNoteAsync(candidate.Id, note.Id, true, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        return CandidateNoteResponse.From(stored);
    }
}
