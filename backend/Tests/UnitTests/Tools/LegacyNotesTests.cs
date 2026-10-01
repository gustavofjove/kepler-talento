using KeplerTalento.Tools.DataMigration.Loading;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

/// <summary>
/// How the legacy status and availability survive the load as note text (KTL-36 design D9).
/// The separators are fixed here because a re-run must recompose exactly the same notes.
/// </summary>
public sealed class LegacyNotesTests
{
    [Fact]
    public void Status_and_availability_follow_the_notes_after_a_blank_line()
    {
        var notes = LegacyNotes.Compose("Perfil senior", "hired", "Incorporación en enero");

        Assert.Equal(
            "Perfil senior\n\nEstado en Access: Contratado.\nDisponibilidad en Access: Incorporación en enero",
            notes);
    }

    [Fact]
    public void A_new_status_and_a_blank_availability_leave_the_notes_exactly_as_they_were()
    {
        Assert.Equal("Perfil senior\n", LegacyNotes.Compose("Perfil senior\n", "new", "   "));
        Assert.Equal(string.Empty, LegacyNotes.Compose(string.Empty, "new", string.Empty));
    }

    [Fact]
    public void Without_notes_the_legacy_lines_stand_alone()
    {
        Assert.Equal("Estado en Access: En proceso.", LegacyNotes.Compose("", " in_process ", ""));
        Assert.Equal("Disponibilidad en Access: Inmediata", LegacyNotes.Compose("  ", "new", " Inmediata "));
    }

    [Theory]
    [InlineData("available", "Disponible")]
    [InlineData("in_process", "En proceso")]
    [InlineData("hired", "Contratado")]
    [InlineData("rejected", "Descartado")]
    public void Each_legacy_status_other_than_new_is_kept_with_its_access_label(string status, string label)
    {
        Assert.Equal($"Notas\n\nEstado en Access: {label}.", LegacyNotes.Compose("Notas", status, ""));
    }

    [Fact]
    public void Composing_is_a_pure_function_of_the_row()
    {
        var first = LegacyNotes.Compose("Perfil senior", "hired", "Incorporación en enero");
        var second = LegacyNotes.Compose("Perfil senior", "hired", "Incorporación en enero");

        Assert.Equal(first, second);
    }

    [Fact]
    public void The_legacy_status_codes_stay_validated()
    {
        Assert.Equal(["new", "available", "in_process", "hired", "rejected"], LegacyCandidateStatuses.All);
        Assert.False(LegacyCandidateStatuses.IsKnown("archived"));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNotes.Compose("", "archived", ""));
    }
}
