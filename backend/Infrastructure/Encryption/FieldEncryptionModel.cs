using KeplerTalento.Application.Abstractions.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// Marks and wires encrypted columns (KTL-33 design decision 1).
/// </summary>
/// <remarks>
/// Entity configurations only mark a property with <see cref="IsEncrypted{T}"/>; the converter
/// is attached afterwards by <see cref="Apply"/>, because configurations are created without
/// dependencies and the protector comes from the context's options. Every encrypted property
/// is therefore discoverable from the model, which is what the query guard and the backfill read.
/// </remarks>
public static class FieldEncryptionModel
{
    public const string ContextAnnotation = "Ktl:EncryptedContext";
    public const string MaxLengthAnnotation = "Ktl:EncryptedMaxLength";
    public const string RequireTextAnnotation = "Ktl:EncryptedRequireText";

    /// <summary>
    /// Stores the property encrypted. <paramref name="maxLength"/> is the plaintext limit the
    /// column width used to enforce; null for columns that were unbounded.
    /// <paramref name="requireText"/> replaces a former <c>char_length(...) &gt; 0</c> check,
    /// which cannot see through ciphertext.
    /// </summary>
    public static PropertyBuilder<T> IsEncrypted<T>(
        this PropertyBuilder<T> builder,
        string table,
        int? maxLength = null,
        bool requireText = false)
    {
        if (builder.Metadata.ClrType != typeof(string))
        {
            throw new InvalidOperationException("Only string properties can be encrypted.");
        }
        builder.HasAnnotation(ContextAnnotation, FieldContext.For(table, builder.Metadata.Name).Value);
        if (maxLength is not null)
        {
            builder.HasAnnotation(MaxLengthAnnotation, maxLength.Value);
        }
        if (requireText)
        {
            builder.HasAnnotation(RequireTextAnnotation, true);
        }
        // The envelope is longer than the plaintext; the limit moves to the application.
        builder.HasColumnType("text");
        return builder;
    }

    public const string FilterDocumentAnnotation = "Ktl:EncryptedFilterDocument";

    /// <summary>
    /// Stores a search filter document with its free-text member encrypted (design decision 7).
    /// The column stays <c>jsonb</c>; it is not one of <see cref="EncryptedProperties"/>.
    /// </summary>
    public static PropertyBuilder<string> HasEncryptedFilterText(this PropertyBuilder<string> builder, string table)
    {
        builder.HasAnnotation(FilterDocumentAnnotation, FieldContext.For(table, $"{builder.Metadata.Name}.text").Value);
        return builder;
    }

    public static IEnumerable<IReadOnlyProperty> EncryptedProperties(IReadOnlyModel model) =>
        model.GetEntityTypes()
            .SelectMany(entity => entity.GetDeclaredProperties())
            .Where(property => property.FindAnnotation(ContextAnnotation) is not null);

    public static FieldContext ContextOf(IReadOnlyProperty property) =>
        new((string)property.FindAnnotation(ContextAnnotation)!.Value!);

    public static int? MaxLengthOf(IReadOnlyProperty property) =>
        (int?)property.FindAnnotation(MaxLengthAnnotation)?.Value;

    public static bool RequiresText(IReadOnlyProperty property) =>
        property.FindAnnotation(RequireTextAnnotation)?.Value is true;

    internal static void Apply(ModelBuilder modelBuilder, IFieldProtector protector)
    {
        foreach (var property in EncryptedProperties(modelBuilder.Model).Cast<IMutableProperty>().ToList())
        {
            property.SetValueConverter(new EncryptedStringConverter(
                protector,
                ContextOf(property),
                MaxLengthOf(property),
                RequiresText(property)));
        }
        var filterDocuments = modelBuilder.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetDeclaredProperties())
            .Where(property => property.FindAnnotation(FilterDocumentAnnotation) is not null)
            .ToList();
        foreach (var property in filterDocuments)
        {
            property.SetValueConverter(new EncryptedFilterDocumentConverter(
                protector,
                new FieldContext((string)property.FindAnnotation(FilterDocumentAnnotation)!.Value!)));
        }
    }

    /// <summary>
    /// Supplies the protector and blind index to every <c>ApplicationDbContext</c> built from
    /// these options.
    /// </summary>
    public static DbContextOptionsBuilder UseFieldEncryption(
        this DbContextOptionsBuilder builder,
        IFieldProtector protector,
        IBlindIndex blindIndex)
    {
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new FieldEncryptionOptionsExtension(protector, blindIndex));
        return builder;
    }

    /// <inheritdoc cref="UseFieldEncryption(DbContextOptionsBuilder, IFieldProtector, IBlindIndex)"/>
    public static DbContextOptionsBuilder<TContext> UseFieldEncryption<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        IFieldProtector protector,
        IBlindIndex blindIndex)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)builder).UseFieldEncryption(protector, blindIndex);
        return builder;
    }

    public static IFieldProtector ProtectorFrom(IDbContextOptions options) =>
        options.FindExtension<FieldEncryptionOptionsExtension>()?.Protector ?? UnavailableFieldProtector.Instance;

    public static IBlindIndex? BlindIndexFrom(IDbContextOptions options) =>
        options.FindExtension<FieldEncryptionOptionsExtension>()?.BlindIndex;
}

/// <summary>
/// Encrypts on the way to PostgreSQL and decrypts on the way back, for tracked entities and
/// scalar projections alike.
/// </summary>
public sealed class EncryptedStringConverter(
    IFieldProtector protector,
    FieldContext context,
    int? maxLength,
    bool requireText = false)
    : ValueConverter<string, string>(
        value => Protect(protector, context, maxLength, requireText, value),
        value => protector.Unprotect(value, context))
{
    // Backstop for writers that skip the validators (the legacy loader, direct test writes):
    // nothing may store what the column's width or check refused before encryption.
    private static string Protect(IFieldProtector protector, FieldContext context, int? maxLength, bool requireText, string value)
    {
        if (maxLength is not null && value.Length > maxLength.Value)
        {
            throw new InvalidOperationException($"A value for {context} exceeds its {maxLength} character limit.");
        }
        if (requireText && value.Length == 0)
        {
            throw new InvalidOperationException($"A value for {context} is required.");
        }
        return protector.Protect(value, context);
    }
}

/// <summary>
/// The protector of a context built without <see cref="FieldEncryptionModel.UseFieldEncryption"/>.
/// Fails closed: nothing is ever written or read in clear by accident.
/// </summary>
public sealed class UnavailableFieldProtector : IFieldProtector
{
    public static readonly UnavailableFieldProtector Instance = new();

    public string Protect(string plaintext, FieldContext context) => throw NotConfigured();

    public string Unprotect(string envelope, FieldContext context) => throw NotConfigured();

    public bool IsProtectedWithActiveKey(string value) => throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Field encryption is not configured for this database context.");
}

internal sealed class FieldEncryptionOptionsExtension(IFieldProtector protector, IBlindIndex blindIndex) : IDbContextOptionsExtension
{
    private ExtensionInfo? _info;

    public IFieldProtector Protector { get; } = protector;

    public IBlindIndex BlindIndex { get; } = blindIndex;

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "FieldEncryption ";

        // The protector changes the model, not the internal services, so every context can share
        // one service provider; the model cache key tells the models apart.
        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["FieldEncryption"] = "1";
    }
}

/// <summary>
/// One cached model per protector instance. Converters are baked into the model, so two
/// contexts with different keys (tests, the backfill) must never share one.
/// </summary>
internal sealed class FieldEncryptionModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), FieldEncryptionModel.ProtectorFrom(context.GetService<IDbContextOptions>()), designTime);
}
