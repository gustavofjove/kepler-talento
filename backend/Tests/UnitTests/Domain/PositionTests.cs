using KeplerTalento.Domain.Positions;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class PositionTests
{
    [Fact]
    public void A_new_position_is_trimmed_normalized_and_open()
    {
        var now = DateTimeOffset.UtcNow;
        var position = new Position(Guid.NewGuid(), "  Programador SÉNIOR ", "<p>Descripción</p>", " Madrid ", "{}", 1, now);
        Assert.Equal("Programador SÉNIOR", position.Title);
        Assert.Equal("programador senior", position.NormalizedTitle);
        Assert.Equal("Madrid", position.Location);
        Assert.Equal(PositionStatuses.Open, position.Status);
    }

    [Fact]
    public void Closing_and_reopening_keep_requirements()
    {
        var position = new Position(Guid.NewGuid(), "Analista", "", "", "{\"version\":1}", 1, DateTimeOffset.UtcNow);
        var requirements = position.Requirements;
        position.Update(position.Title, position.Description, position.Location, PositionStatuses.Closed, requirements, 1, DateTimeOffset.UtcNow);
        position.Update(position.Title, position.Description, position.Location, PositionStatuses.Open, requirements, 1, DateTimeOffset.UtcNow);
        Assert.Equal(requirements, position.Requirements);
    }

    [Fact]
    public void Unknown_status_and_bounded_fields_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(Guid.NewGuid(), "", "", "", "{}", 1, DateTimeOffset.UtcNow));
        var position = new Position(Guid.NewGuid(), "Analista", "", "", "{}", 1, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => position.Update("Analista", "", "", "paused", "{}", 1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.Update(new string('x', 201), "", "", "open", "{}", 1, DateTimeOffset.UtcNow));
    }
}
