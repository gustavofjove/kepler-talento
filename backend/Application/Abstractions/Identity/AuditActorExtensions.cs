using KeplerTalento.Domain.Auditing;

namespace KeplerTalento.Application.Abstractions.Identity;

public static class AuditActorExtensions
{
    /// <summary>
    /// The audit actor for a request: the caller's internal user id.
    /// </summary>
    /// <remarks>
    /// Fails closed. An audited operation whose caller has no stored user behind it — a
    /// development actor, or a test double without a user id — is refused rather than recorded as
    /// the system or as nobody, because either would make the trail lie about who acted
    /// (KTL-19 design D1). Background work with no person behind it uses
    /// <see cref="AuditActor.System"/> explicitly instead of calling this.
    /// </remarks>
    public static AuditActor ToAuditActor(this ICurrentActor actor) =>
        actor.IsAuthenticated && actor.UserId is { } userId && userId != Guid.Empty
            ? AuditActor.User(userId)
            : throw new InvalidOperationException(
                "An audited operation requires a caller with an internal user id.");
}
