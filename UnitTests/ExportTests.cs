using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.SessionHistory;
using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.SpeebrunConsistencyTracker.Export;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class CsvFieldTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("0.555", "0.555")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_value_with_nothing_special_in_it_is_left_alone(string? value, string expected)
        => Assert.Equal(expected, Csv.Field(value));

    // A comma inside a value silently becomes a column break, which the file cannot show.
    [Theory]
    [InlineData("Two peaks: 0.983, 1.150", "\"Two peaks: 0.983, 1.150\"")]
    [InlineData("he said \"go\"", "\"he said \"\"go\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("carriage\rreturn", "\"carriage\rreturn\"")]
    public void A_value_that_would_break_the_row_is_quoted(string value, string expected)
        => Assert.Equal(expected, Csv.Field(value));

    // On the clipboard the separator is a tab, so a comma is an ordinary character there.
    [Theory]
    [InlineData("Two peaks: 0.983, 1.150", "Two peaks: 0.983, 1.150")]
    [InlineData("a\tb", "\"a\tb\"")]
    [InlineData("he said \"go\"", "\"he said \"\"go\"\"\"")]
    public void Only_the_separator_in_use_forces_quotes(string value, string expected)
        => Assert.Equal(expected, Csv.Field(value, Csv.ClipboardSeparator));
}

public class SanitizeFileNameTests
{
    [Fact]
    public void A_name_with_nothing_illegal_in_it_is_left_alone()
        => Assert.Equal("Celeste_1-ForsakenCity", DataExporter.SanitizeFileName("Celeste_1-ForsakenCity"));

    [Fact]
    public void Path_separators_and_other_illegal_characters_are_dropped()
    {
        string sanitized = DataExporter.SanitizeFileName("Pack/A:B\\room");

        Assert.DoesNotContain('/', sanitized);
        Assert.DoesNotContain('\\', sanitized);
        Assert.Equal("PackABroom", sanitized);
    }

    // A directory named "foo." or "foo " is not creatable on Windows.
    [Theory]
    [InlineData("trailing.", "trailing")]
    [InlineData("trailing ", "trailing")]
    [InlineData("trailing . . ", "trailing")]
    public void Trailing_dots_and_spaces_go(string input, string expected)
        => Assert.Equal(expected, DataExporter.SanitizeFileName(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_name_yields_an_empty_string(string? input)
        => Assert.Equal(string.Empty, DataExporter.SanitizeFileName(input));
}

public class UniqueTimestampTests
{
    // Two exports in the same second used to overwrite each other: the stamp is second-resolution.
    [Fact]
    public void The_plain_stamp_is_used_when_nothing_is_in_the_way()
    {
        string stamp = DataExporter.UniqueTimestamp("any", _ => false);

        Assert.Matches(@"^\d{8}_\d{6}$", stamp);
    }

    [Fact]
    public void A_taken_stamp_is_suffixed_until_it_is_free()
    {
        HashSet<string> taken = [];
        string first = DataExporter.UniqueTimestamp("dir", taken.Contains);
        taken.Add(Path.Combine("dir", $"{first}_Metrics.csv"));

        string second = DataExporter.UniqueTimestamp("dir", taken.Contains);
        taken.Add(Path.Combine("dir", $"{second}_Metrics.csv"));

        string third = DataExporter.UniqueTimestamp("dir", taken.Contains);

        Assert.Equal($"{first}_2", second);
        Assert.Equal($"{first}_3", third);
    }
}

// Finishing the last room moves the session on to the room after it, and the next reset records a
// DNF there. The data is right -- the player did reset in that room -- but it is outside the
// segment, and the history exported every room ever reached, so each completed run read as a
// death in a room that is not part of the segment.
public class SessionHistoryExporterTests
{
    private static readonly long S = System.TimeSpan.TicksPerSecond;

    // MaxRoomCount is refreshed before every export (DataExporter -> UpdateRoomCount), and it is
    // what reached the extra room, so the test refreshes it too.
    private static string[] Lines(PracticeSession session)
    {
        session.RecomputeMaxRoomCount();
        return SessionHistoryExporter.ExportSessionToCsv(session)
            .Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
    }

    private static void Run(PracticeSession session, params long[] seconds)
    {
        foreach (long s in seconds) session.CompleteRoom(new TimeTicks(s * S));
    }

    [Fact]
    public void A_completed_run_exports_the_segments_rooms_and_no_death_after_it()
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 3));
        Run(session, 1, 2, 3);
        session.StartNewAttempt();
        Run(session, 1, 2, 3);
        session.StartNewAttempt();

        Assert.Equal(
            ["Attempt,R1,R2,R3,Segment", "1,1.000,2.000,3.000,6.000", "2,1.000,2.000,3.000,6.000"],
            Lines(session));
    }

    // A spreadsheet splits a paste on tabs; with commas the whole row lands in one cell.
    [Fact]
    public void The_clipboard_separator_replaces_every_comma()
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 2));
        Run(session, 1, 2);
        session.StartNewAttempt();
        session.RecomputeMaxRoomCount();

        string[] lines = SessionHistoryExporter.ExportSessionToCsv(session, Csv.ClipboardSeparator)
            .Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

        Assert.Equal(["Attempt\tR1\tR2\tSegment", "1\t1.000\t2.000\t3.000"], lines);
    }

    [Fact]
    public void A_reset_inside_the_segment_still_exports_as_DNF()
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 3));
        Run(session, 1);
        session.CompleteRoom(new TimeTicks(2 * S));
        session.StartNewAttempt();   // reset in R3, after R1 and R2
        Run(session, 1, 2, 3);

        Assert.Equal(
            ["Attempt,R1,R2,R3,Segment", "1,1.000,2.000,DNF,", "2,1.000,2.000,3.000,6.000"],
            Lines(session));
    }
}
