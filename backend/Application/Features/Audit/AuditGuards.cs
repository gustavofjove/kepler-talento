using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;

namespace KeplerTalento.Application.Features.Audit;

/// <summary>Authorization for the audit read surface.</summary>
/// <remarks>
/// Only <see cref="Permissions.AuditRead"/> opens the trail. Holding every candidate, catalog and
/// document permission does not: the trail records what those actors did, and a trail readable by
/// everyone it records is not a control.
/// </remarks>
public static class AuditGuards
{
    public static void RequireRead(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.AuditRead))
        {
            throw new ForbiddenException();
        }
    }
}
