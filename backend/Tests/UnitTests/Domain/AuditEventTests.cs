using System.Reflection;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class AuditEventTests
{
    private static readonly Guid UserId = Guid.Parse("01932f00-0000-7000-8000-00000000e001");

    [Fact]
    public void A_user_event_records_the_internal_user_id()
    {
        var audit = New(AuditActor.User(UserId));

        Assert.Equal(AuditEvent.UserActorKind, audit.ActorKind);
        Assert.Equal(UserId, audit.ActorUserId);
        Assert.Equal(AuditActor.User(UserId), audit.Actor);
    }

    [Fact]
    public void A_system_event_records_the_system_actor_and_no_user()
    {
        var audit = New(AuditActor.System);

        Assert.Equal(AuditEvent.SystemActorKind, audit.ActorKind);
        Assert.Null(audit.ActorUserId);
        Assert.Equal(AuditActorKind.System, audit.Actor.Kind);
    }

    [Fact]
    public void The_actor_is_a_required_non_optional_constructor_parameter()
    {
        var constructor = Assert.Single(typeof(AuditEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var actor = Assert.Single(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(AuditActor));

        // A missing actor is a compile error, not a silent null (design D1).
        Assert.False(actor.IsOptional);
        Assert.Throws<ArgumentNullException>(() => New(null!));
    }

    [Fact]
    public void No_audit_writing_port_accepts_a_string_identity_in_place_of_an_actor()
    {
        // Enumerates every member of the ports that write audit events: each must take the
        // actor as an AuditActor, so a new operation cannot pass an external key or nothing.
        var writers = typeof(IDocumentRepository).GetMethods()
            .Where(method => method.GetParameters().Any(parameter => parameter.Name is "correlationId"))
            .ToList();

        Assert.NotEmpty(writers);
        Assert.All(writers, method =>
        {
            Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(AuditActor));
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                parameter.Name!.Contains("ExternalKey", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void The_unknown_actor_cannot_be_constructed_by_writing_code()
    {
        var type = typeof(AuditActor);

        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var reachable = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == type)
            .Select(property => (AuditActor)property.GetValue(null)!);
        Assert.DoesNotContain(reachable, actor => actor.Kind == AuditActorKind.Unknown);
        Assert.Throws<ArgumentException>(() => AuditActor.User(Guid.Empty));
    }

    [Fact]
    public void A_historic_row_without_actor_columns_reads_back_as_unknown_and_not_as_system()
    {
        // Materialised the way EF does: the private constructor, no actor columns set.
        var historic = (AuditEvent)Activator.CreateInstance(typeof(AuditEvent), nonPublic: true)!;

        Assert.Equal(AuditActorKind.Unknown, historic.Actor.Kind);
        Assert.NotEqual(AuditActor.System, historic.Actor);
    }

    [Fact]
    public void A_caller_without_a_stored_user_cannot_become_an_audit_actor()
    {
        Assert.Throws<InvalidOperationException>(() => new Caller(true, null).ToAuditActor());
        Assert.Throws<InvalidOperationException>(() => new Caller(false, UserId).ToAuditActor());
        Assert.Equal(AuditActor.User(UserId), new Caller(true, UserId).ToAuditActor());
    }

    [Theory]
    [InlineData("candidate.viewed")]
    [InlineData("CANDIDATE.READ")]
    [InlineData("")]
    public void An_event_type_outside_the_catalogue_is_rejected(string eventType)
    {
        Assert.Throws<ArgumentException>(() =>
            new AuditEvent(Guid.CreateVersion7(), eventType, "subject", "corr", DateTimeOffset.UtcNow, AuditActor.System));
    }

    [Fact]
    public void The_catalogue_contains_every_feature_event_type_exactly_once()
    {
        var declared = typeof(AuditEvent).Assembly.GetTypes()
            .Where(type => type.IsAbstract && type.IsSealed && type.Name.EndsWith("AuditEvents", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared.Order(), AuditEventTypes.All.Order());
        Assert.Equal(AuditEventTypes.All.Count, AuditEventTypes.All.Distinct().Count());
        Assert.Contains(CandidateAuditEvents.Read, AuditEventTypes.All);
    }

    [Fact]
    public void An_audit_event_has_no_member_that_could_hold_identity_or_candidate_data()
    {
        var members = typeof(AuditEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            new[] { "Actor", "ActorKind", "ActorUserId", "CorrelationId", "CreatedAtUtc", "EventType", "Id", "OutcomeCode", "SubjectId" },
            members);
        Assert.Equal(new[] { "Kind", "UserId" }, typeof(AuditActor).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order());
    }

    private static AuditEvent New(AuditActor actor) =>
        new(Guid.CreateVersion7(), CandidateAuditEvents.Updated, "subject", "corr", DateTimeOffset.UtcNow, actor);

    private sealed class Caller(bool authenticated, Guid? userId) : ICurrentActor
    {
        public string? ExternalKey => "caller";
        public Guid? UserId => userId;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) => true;
    }
}
