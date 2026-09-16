using System.Text.Json;

using KeplerTalento.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>Roles: named permission sets an administrator governs.</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public const string Table = "ADM_Roles";
    public const string UniqueNameIndex = "UX_ADM_Roles_Name";

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable(Table, table =>
        {
            // The name pattern is the database's business too: it is a foreign key target, so a
            // row that slipped past the domain would be referenced by user rows forever.
            table.HasCheckConstraint(
                $"CK_{Table}_Name",
                "\"Name\" ~ '^[a-z0-9_]+$'");
            table.HasCheckConstraint(
                $"CK_{Table}_Label",
                "char_length(btrim(\"Label\")) > 0");
            // A role that grants nothing is not a role. jsonb_typeof pins it to an array and
            // jsonb_array_length pins it to a non-empty one.
            table.HasCheckConstraint(
                $"CK_{Table}_Permissions",
                "jsonb_typeof(\"Permissions\") = 'array' AND jsonb_array_length(\"Permissions\") > 0");
            table.HasCheckConstraint(
                $"CK_{Table}_Timestamps",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(RoleName.MaximumLength).IsRequired();
        builder.Property(role => role.Label).HasMaxLength(120).IsRequired();
        builder.Property(role => role.IsSystem).IsRequired();
        builder.Property(role => role.IsActive).IsRequired();
        builder.Property(role => role.CreatedAtUtc).IsRequired();
        builder.Property(role => role.UpdatedAtUtc).IsRequired();
        builder.Property(role => role.Version).IsRowVersion();

        // The permission set is read whole and never queried across, so a jsonb document is the
        // right shape — the same reasoning SearchPreset.Filters records. The value comparer is
        // what makes EF notice that ReplacePermissions changed the collection.
        var permissions = builder.Property(role => role.Permissions)
            .HasColumnName("Permissions")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null)!,
                new ValueComparer<IReadOnlyList<string>>(
                    (left, right) => left!.SequenceEqual(right!, StringComparer.Ordinal),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                    value => value.ToList()));

        // The property is read-only over a private List; EF writes the backing field.
        permissions.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Decided by the database, not by a prior read: two concurrent creations of the same
        // name both find it free and only one can store it.
        builder.HasIndex(role => role.Name).IsUnique().HasDatabaseName(UniqueNameIndex);
    }
}
