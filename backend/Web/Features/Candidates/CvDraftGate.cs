using KeplerTalento.Application.Abstractions.CvExtraction;

namespace KeplerTalento.Web.Features.Candidates;

/// <summary>
/// Caps the CV drafts in flight, and with them the in-memory buffers (KTL-32 design D3).
/// </summary>
/// <remarks>
/// Entered inside the endpoint, after the permission check, so an unauthorized caller is refused
/// as unauthorized whatever the load, and never takes a slot. There is no queue: an extra request
/// is refused at once and the SPA offers a retry.
/// </remarks>
public sealed class CvDraftGate(CvDraftOptions options) : IDisposable
{
    private readonly SemaphoreSlim _slots = new(options.MaxConcurrent, options.MaxConcurrent);

    public bool TryEnter() => _slots.Wait(0);

    public void Exit() => _slots.Release();

    public void Dispose() => _slots.Dispose();
}
