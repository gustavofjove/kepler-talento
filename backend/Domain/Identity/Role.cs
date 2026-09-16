using System.Text.RegularExpressions;

namespace KeplerTalento.Domain.Identity;

/// <summary>Validation of a role's immutable name.</summary>
public static partial class RoleName
{
    public const int MaximumLength = 64;

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaximumLength && Pattern().IsMatch(name);
}

/// <summary>
/// A named set of permissions. The catalogue of permission strings is code; which of them a
/// role holds is data, so an installation can reshape its roles without a deployment.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Name"/> is settable only at construction (design D7). <c>ADM_Users.RoleName</c> is
/// a foreign key to it, so a rename would mean cascading updates on a natural key; the label is
/// what changes when an installation wants a role to read differently.
/// </para>
/// <para>
/// Roles are deactivated, never deleted (non-negotiable 5). A user holding an inactive role is
/// still authenticated but holds no permissions, which is the difference between "cannot do
/// anything" and "is not here".
/// </para>
/// </remarks>
public sealed class Role
{
    private readonly List<string> permissions = [];

    private Role() { }

    public Role(
        Guid id,
        string name,
        string label,
        bool isSystem,
        IEnumerable<string> permissions,
        DateTimeOffset createdAtUtc)
    {
        if (!RoleName.IsValid(name))
        {
            throw new ArgumentException("A role name is lowercase letters, digits and underscores.", nameof(name));
        }

        Id = id;
        Name = name;
        IsSystem = isSystem;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ApplyLabel(label);
        ApplyPermissions(permissions);
    }

    public Guid Id { get; private set; }

    /// <summary>Immutable after construction: <c>ADM_Users.RoleName</c> references it.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>How the role is shown to an administrator. Freely editable, including on system roles.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>
    /// Seeded by the migration. A system role may have its label and permission set changed but
    /// may not be renamed, emptied or deactivated, so an installation cannot edit itself into a
    /// state where users hold a role that means nothing.
    /// </summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyList<string> Permissions => permissions;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary><c>xmin</c>, as every other aggregate but <see cref="Search.SearchPreset"/> uses.</summary>
    public uint Version { get; private set; }

    public void Relabel(string label, DateTimeOffset updatedAtUtc)
    {
        ApplyLabel(label);
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Replaces the whole permission set. The caller validates the values against
    /// <c>Permissions.All</c> and enforces the lockout and system-role rules; the aggregate
    /// enforces only that a role is never meaningless.
    /// </summary>
    public void ReplacePermissions(IEnumerable<string> replacement, DateTimeOffset updatedAtUtc)
    {
        ApplyPermissions(replacement);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Reactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = true;
        UpdatedAtUtc = updatedAtUtc;
    }

    public bool Grants(string permission) =>
        IsActive && permissions.Contains(permission, StringComparer.Ordinal);

    private void ApplyLabel(string label)
    {
        var trimmed = (label ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 120)
        {
            throw new ArgumentException("A role needs a non-blank, bounded label.", nameof(label));
        }
        Label = trimmed;
    }

    private void ApplyPermissions(IEnumerable<string> replacement)
    {
        // Ordinal-distinct and sorted, so two roles holding the same set store the same
        // document and a diff of the column is readable.
        var distinct = replacement
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (distinct.Count == 0)
        {
            throw new ArgumentException("A role holds at least one permission.", nameof(replacement));
        }

        permissions.Clear();
        permissions.AddRange(distinct);
    }
}
