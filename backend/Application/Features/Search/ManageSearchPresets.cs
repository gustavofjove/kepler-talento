using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Search;
using MediatR;

namespace KeplerTalento.Application.Features.Search;

public sealed record ListSearchPresetsQuery : IRequest<IReadOnlyList<SearchPresetResponse>>;

public sealed record CreateSearchPresetCommand(string? Name, SearchFiltersInput? Filters)
    : IRequest<SearchPresetResponse>;

public sealed record UpdateSearchPresetCommand(Guid Id, string? Name, SearchFiltersInput? Filters)
    : IRequest<SearchPresetResponse>;

public sealed record DeleteSearchPresetCommand(Guid Id) : IRequest;

/// <summary>Applying a preset: it answers with its filters and records that it was used.</summary>
public sealed record UseSearchPresetCommand(Guid Id) : IRequest<SearchPresetResponse>;

public sealed class ListSearchPresetsHandler(ISearchPresetRepository presets, ICurrentActor actor)
    : IRequestHandler<ListSearchPresetsQuery, IReadOnlyList<SearchPresetResponse>>
{
    public async Task<IReadOnlyList<SearchPresetResponse>> Handle(
        ListSearchPresetsQuery request,
        CancellationToken cancellationToken)
    {
        var owner = SearchGuards.RequireOwner(actor);
        var stored = await presets.ListAsync(owner, cancellationToken);
        return [.. stored.Select(SearchPresetMapping.ToResponse)];
    }
}

public sealed class CreateSearchPresetHandler(ISearchPresetRepository presets, ICurrentActor actor)
    : IRequestHandler<CreateSearchPresetCommand, SearchPresetResponse>
{
    public async Task<SearchPresetResponse> Handle(
        CreateSearchPresetCommand request,
        CancellationToken cancellationToken)
    {
        var owner = SearchGuards.RequireOwner(actor);
        var (name, filters) = SearchPresetMapping.Validate(request.Name, request.Filters);
        var now = DateTimeOffset.UtcNow;
        var preset = new SearchPreset(
            Guid.CreateVersion7(),
            owner,
            name,
            SearchFilterDocument.Serialize(filters),
            SearchFilterNormalization.FilterSchemaVersion,
            now);
        presets.Add(preset);
        // No read-then-check for the name: the unique index is the only answer that holds
        // under two concurrent creations, so the conflict is detected where it is decided.
        return await presets.SaveAsync(cancellationToken) == SearchPresetSaveOutcome.NameConflict
            ? throw SearchGuards.PresetNameConflict()
            : SearchPresetMapping.ToResponse(preset);
    }
}

public sealed class UpdateSearchPresetHandler(ISearchPresetRepository presets, ICurrentActor actor)
    : IRequestHandler<UpdateSearchPresetCommand, SearchPresetResponse>
{
    public async Task<SearchPresetResponse> Handle(
        UpdateSearchPresetCommand request,
        CancellationToken cancellationToken)
    {
        var owner = SearchGuards.RequireOwner(actor);
        var (name, filters) = SearchPresetMapping.Validate(request.Name, request.Filters);
        var preset = await presets.FindAsync(owner, request.Id, cancellationToken)
            ?? throw SearchGuards.PresetNotFound();
        var now = DateTimeOffset.UtcNow;
        // Identifier, owner and creation time are not part of the update surface: they are
        // not writable fields that happen to be left alone, they are simply not writable.
        preset.Rename(name, now);
        preset.ReplaceFilters(
            SearchFilterDocument.Serialize(filters),
            SearchFilterNormalization.FilterSchemaVersion,
            now);
        return await presets.SaveAsync(cancellationToken) == SearchPresetSaveOutcome.NameConflict
            ? throw SearchGuards.PresetNameConflict()
            : SearchPresetMapping.ToResponse(preset);
    }
}

public sealed class DeleteSearchPresetHandler(ISearchPresetRepository presets, ICurrentActor actor)
    : IRequestHandler<DeleteSearchPresetCommand>
{
    public async Task Handle(DeleteSearchPresetCommand request, CancellationToken cancellationToken)
    {
        var owner = SearchGuards.RequireOwner(actor);
        var preset = await presets.FindAsync(owner, request.Id, cancellationToken)
            ?? throw SearchGuards.PresetNotFound();
        presets.Remove(preset);
        await presets.SaveAsync(cancellationToken);
    }
}

public sealed class UseSearchPresetHandler(ISearchPresetRepository presets, ICurrentActor actor)
    : IRequestHandler<UseSearchPresetCommand, SearchPresetResponse>
{
    public async Task<SearchPresetResponse> Handle(
        UseSearchPresetCommand request,
        CancellationToken cancellationToken)
    {
        var owner = SearchGuards.RequireOwner(actor);
        var preset = await presets.FindAsync(owner, request.Id, cancellationToken)
            ?? throw SearchGuards.PresetNotFound();
        // Reading the filters before recording the use: a stored value the application can
        // no longer understand is refused, and refusing it must not first leave a "used"
        // timestamp behind for an apply that did not happen.
        var response = SearchPresetMapping.ToResponse(preset);
        preset.MarkUsed(DateTimeOffset.UtcNow);
        await presets.SaveAsync(cancellationToken);
        return response with { UpdatedAt = preset.UpdatedAtUtc, LastUsedAt = preset.LastUsedAtUtc };
    }
}

internal static class SearchPresetMapping
{
    /// <summary>
    /// The one place a preset write is checked. Name and filters are validated together so a
    /// request with both wrong is refused once, naming both.
    /// </summary>
    public static (string Name, SearchFiltersValue Filters) Validate(
        string? name,
        SearchFiltersInput? filters)
    {
        var issues = new List<ValidationIssue>();
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            issues.Add(new ValidationIssue(
                "Name",
                SearchErrors.PresetNameRequired,
                SearchErrors.PresetNameRequiredMessage));
        }
        else if (trimmed.Length > SearchPresetName.MaximumLength)
        {
            issues.Add(new ValidationIssue(
                "Name",
                SearchErrors.PresetNameTooLong,
                SearchErrors.PresetNameTooLongMessage));
        }
        var value = SearchFilterNormalization.TryNormalize(filters, "Filters", issues);
        return issues.Count > 0 ? throw new RequestValidationException(issues) : (trimmed, value);
    }

    /// <summary>
    /// The owner identifier is deliberately absent from the response. A preset is private,
    /// and its owner already knows who they are.
    /// </summary>
    public static SearchPresetResponse ToResponse(SearchPreset preset) => new(
        preset.Id,
        preset.Name,
        SearchFilterDocument.Parse(preset.Filters).ToInput(),
        preset.CreatedAtUtc,
        preset.UpdatedAtUtc,
        preset.LastUsedAtUtc);
}
