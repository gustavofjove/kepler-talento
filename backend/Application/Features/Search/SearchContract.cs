using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;

namespace KeplerTalento.Application.Features.Search;

/// <summary>
/// One page of results plus what it took to produce them.
/// </summary>
/// <remarks>
/// <paramref name="Page"/> and <paramref name="PageSize"/> report the values the server
/// actually applied, not the ones the caller sent, so a request that omitted them learns
/// the defaults instead of having to know them.
///
/// <paramref name="TotalCount"/> and <paramref name="Items"/> are two statements under
/// read-committed isolation. For unchanged data they agree exactly; while another user
/// writes, the count may describe a marginally different population than the page. That is
/// documented rather than prevented: holding a repeatable-read transaction across both
/// costs more than the discrepancy is worth at this scale.
/// </remarks>
public sealed record SearchPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// One search hit. Deliberately not a candidate: no relation collections, no notes, no
/// consent or retention metadata, no storage key, filename or scan state.
/// </summary>
/// <remarks>
/// <paramref name="PrimaryCvDocumentId"/> is an opaque identifier and nothing more. It lets
/// the results row offer "Abrir CV", which then goes through the document capability's own
/// permission and scan-state checks; it confers no right to read the bytes and reveals
/// nothing about where they live.
/// </remarks>
public sealed record CandidateSearchItem(
    Guid CandidateId,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    string Status,
    bool HasPrimaryCv,
    Guid? PrimaryCvDocumentId,
    DateTimeOffset UpdatedAt);

/// <summary>A saved search as its owner sees it. The owner identifier is never on the wire.</summary>
public sealed record SearchPresetResponse(
    Guid Id,
    string Name,
    SearchFiltersInput Filters,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Bounds the server applies to every search, whatever the caller asks for.</summary>
public static class SearchPaging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    /// <summary>
    /// Applies the defaults for absent values and refuses out-of-range ones. Absent is not
    /// the same as invalid: omitting the page size accepts the default, while asking for a
    /// thousand rows is a request the server will not serve and does not quietly reinterpret.
    /// </summary>
    public static (int Page, int PageSize) Validate(int? page, int? pageSize, List<ValidationIssue> issues)
    {
        var effectivePage = page ?? DefaultPage;
        var effectivePageSize = pageSize ?? DefaultPageSize;
        if (effectivePage < 1)
        {
            issues.Add(new ValidationIssue("Page", SearchErrors.PageInvalid, SearchErrors.PageInvalidMessage));
            effectivePage = DefaultPage;
        }
        if (effectivePageSize is < 1 or > MaximumPageSize)
        {
            issues.Add(new ValidationIssue(
                "PageSize",
                SearchErrors.PageSizeInvalid,
                SearchErrors.PageSizeInvalidMessage));
            effectivePageSize = DefaultPageSize;
        }
        return (effectivePage, effectivePageSize);
    }
}

/// <summary>Authorization and refusals shared by the search and preset slices.</summary>
internal static class SearchGuards
{
    /// <summary>
    /// Reading candidates is the capability searching requires; there is no separate search
    /// permission. <c>view_all_candidates</c> is intentionally not consulted — see the
    /// KTL-10 design: the domain models no ownership, team or assignment from which a
    /// narrower scope could truthfully be derived, so every permitted actor sees the same
    /// eligible population until one exists.
    /// </summary>
    public static void RequireRead(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead))
        {
            throw new ForbiddenException();
        }
    }

    /// <summary>
    /// The owner of a preset is the current actor and can never be supplied by the caller —
    /// a client-chosen owner is a confused deputy waiting to happen.
    /// </summary>
    /// <remarks>
    /// An authenticated actor without a stable key is refused rather than defaulted to the
    /// empty string, which would silently pool every such actor's presets into one shared
    /// owner.
    /// </remarks>
    public static string RequireOwner(ICurrentActor actor)
    {
        RequireRead(actor);
        var owner = actor.ExternalKey?.Trim();
        return string.IsNullOrEmpty(owner)
            ? throw new ForbiddenException(SearchErrors.PresetOwnerUnknown)
            : owner;
    }

    /// <summary>
    /// The single refusal for "this preset is not yours" and "this preset does not exist".
    /// They must be indistinguishable, or the API answers questions about other owners'
    /// presets to anyone willing to guess identifiers.
    /// </summary>
    public static NotFoundException PresetNotFound() =>
        new(SearchErrors.PresetNotFound, SearchErrors.PresetNotFoundMessage);

    public static ConflictException PresetNameConflict() =>
        new(SearchErrors.PresetNameConflict, SearchErrors.PresetNameConflictMessage);
}
