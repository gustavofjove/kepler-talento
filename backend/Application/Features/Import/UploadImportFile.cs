using FluentValidation;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Import;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

public sealed record UploadImportFileCommand(string OriginalFileName, Stream Content) : IRequest<ImportBatchResponse>;

public sealed class UploadImportFileValidator : AbstractValidator<UploadImportFileCommand>
{
    public UploadImportFileValidator()
    {
        RuleFor(command => command.OriginalFileName)
            .NotEmpty()
            .WithErrorCode(ImportErrors.FileMissing)
            .WithMessage("Debe seleccionar un archivo.");
    }
}

/// <summary>
/// Stores an uploaded import file in quarantine and queues its scan.
/// </summary>
/// <remarks>
/// Nothing here reads the file's content beyond copying it into quarantine and hashing it: the
/// file is not parsed, sniffed or validated until the scanner has reported it clean. The response
/// carries the batch and no storage key or path.
/// </remarks>
public sealed class UploadImportFileHandler(
    IImportBatchRepository batches,
    IImportFileStorage importStorage,
    IDocumentStorage storage,
    IOperationRepository operations,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<UploadImportFileCommand, ImportBatchResponse>
{
    public async Task<ImportBatchResponse> Handle(UploadImportFileCommand request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        if (!importStorage.IsAllowedFileName(request.OriginalFileName))
        {
            throw ImportErrors.Validation(
                ImportErrors.FileTypeNotAllowed,
                "Solo se admiten archivos CSV.");
        }

        var batchId = Guid.CreateVersion7();
        var storageKey = importStorage.CreateKey(batchId);
        StoredFile stored;
        try
        {
            stored = await storage.WriteQuarantineAsync(storageKey, request.Content, importStorage.MaximumBytes, cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message == "document.size.exceeded")
        {
            throw ImportErrors.Validation(ImportErrors.FileTooLarge, "El archivo supera el tamaño máximo permitido.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "document.empty")
        {
            throw ImportErrors.Validation(ImportErrors.FileEmpty, "El archivo está vacío.");
        }

        try
        {
            var batch = new ImportBatch(
                batchId,
                storageKey,
                request.OriginalFileName,
                stored.Size,
                stored.Sha256,
                actor.UserId,
                actor.ExternalKey,
                DateTimeOffset.UtcNow);
            batches.Add(batch);
            await batches.SaveAsync(cancellationToken);
            // After the batch row exists, so the operation always has something to act on. A
            // crash between the two leaves an "uploaded" batch that start-up recovery re-queues.
            await operations.EnqueueAsync(
                ImportOperations.Scan,
                correlation.CorrelationId,
                ImportOperations.Key(ImportOperations.Scan, batchId, batch.OperationAttempt),
                cancellationToken);
            return ImportBatchResponse.From(batch, actor, sameFileCommittedAt: null);
        }
        catch
        {
            await storage.DeleteQuarantineIfExistsAsync(storageKey, CancellationToken.None);
            throw;
        }
    }
}
