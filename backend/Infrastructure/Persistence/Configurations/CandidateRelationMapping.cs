using System.Linq.Expressions;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared mapping for the collections a candidate owns. Two rules are enforced here for
/// every relation table so no individual configuration can forget them: the owning
/// candidate is never cascade-deleted (removal is logical, so relations survive it), and
/// a catalog reference is a composite foreign key onto <c>(Id, Family)</c> so it cannot
/// resolve to an entry of the wrong family.
/// </summary>
internal static class CandidateRelationMapping
{
    /// <summary>
    /// The alternate key a relation's composite catalog foreign key points at. A plain
    /// foreign key onto <c>Id</c> alone could not tell a language from a sector.
    /// </summary>
    public const string CatalogAlternateKey = "AK_CAT_CatalogItems_Id_Family";

    /// <summary>
    /// Pins a family column to its single legal value, so the composite foreign key can
    /// only ever resolve within that family.
    /// </summary>
    public static string FamilyCheck(string familyColumnName, string family) =>
        $"\"{familyColumnName}\" = '{family}'";

    public static void ConfigureRelation<TRelation>(
        this EntityTypeBuilder<TRelation> builder,
        string tableName)
        where TRelation : CandidateRelation
    {
        builder.HasKey(relation => relation.Id);
        builder.Property(relation => relation.SourceKey).HasMaxLength(200);
        builder.HasIndex(relation => relation.CandidateId)
            .HasDatabaseName($"IX_{tableName}_CandidateId");
        builder.HasIndex(relation => relation.SourceKey)
            .IsUnique()
            .HasFilter("\"SourceKey\" IS NOT NULL")
            .HasDatabaseName($"UX_{tableName}_SourceKey");
        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(relation => relation.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Wires one catalog reference as a composite foreign key spanning identifier and
    /// family together.
    /// </summary>
    public static void HasCatalogReference<TRelation>(
        this EntityTypeBuilder<TRelation> builder,
        Expression<Func<TRelation, object?>> foreignKey,
        Expression<Func<TRelation, string>> familyProperty)
        where TRelation : class
    {
        builder.Property(familyProperty).HasMaxLength(40).IsRequired();
        builder.HasOne<CatalogItem>()
            .WithMany()
            .HasForeignKey(foreignKey)
            .HasPrincipalKey(item => new { item.Id, item.Family })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
