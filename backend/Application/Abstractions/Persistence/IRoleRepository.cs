using KeplerTalento.Domain.Identity;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum RoleSaveOutcome
{
    Saved,

    /// <summary>The unique index on the role name refused the write.</summary>
    NameConflict,

    /// <summary>The role changed or disappeared after the caller read the version it sent.</summary>
    ConcurrencyConflict,
}

/// <summary>Persistence for roles.</summary>
/// <remarks>
/// There is no <c>Remove</c>: roles are deactivated, never deleted, and <c>ktl_runtime</c> holds
/// no <c>DELETE</c> on <c>ADM_Roles</c>.
/// </remarks>
public interface IRoleRepository
{
    /// <summary>Ordered by name. Includes inactive roles: administering them is the point.</summary>
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken);

    Task<Role?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The per-request permission lookup, by the name held on the user row.</summary>
    Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken);

    void Add(Role role);

    void ExpectVersion(Role role, uint version);

    Task<RoleSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
