using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TimeEntryTests
{
    private static string Typed(string keys)
    {
        TimeEntry entry = new();
        foreach (char c in keys) entry.Type(c);
        return entry.Text;
    }

    [Fact]
    public void Digits_colon_and_dot_are_typed_as_they_are() => Assert.Equal("1:23.456", Typed("1:23.456"));

    // AZERTY's unshifted number row types these symbols, not digits, and a player reaching for "1"
    // without Shift gets "&". In a time nothing else could mean them.
    [Theory]
    [InlineData("&é\"'(", "12345")]            // French, first half: the entry holds 9 characters
    [InlineData("-è_çà", "67890")]              // French, second half
    [InlineData("§!", "68")]                   // Belgian differs on 6 and 8
    public void The_AZERTY_number_row_types_its_digits(string keys, string expected) =>
        Assert.Equal(expected, Typed(keys));

    [Theory]
    [InlineData("1:23,456")] // the French decimal separator
    [InlineData("1:23;456")] // the unshifted key under "." on AZERTY
    public void A_comma_or_semicolon_is_the_decimal_point(string keys) => Assert.Equal("1:23.456", Typed(keys));

    [Fact]
    public void Letters_and_other_symbols_are_refused()
    {
        TimeEntry entry = new();
        Assert.False(entry.Type('a'));
        Assert.False(entry.Type(' '));
        Assert.False(entry.Type('/'));
        Assert.Equal("", entry.Text);
    }

    [Fact]
    public void Nothing_is_typed_past_mm_ss_fff() => Assert.Equal("12:34.567", Typed("12:34.5678"));

    [Fact]
    public void Backspace_removes_the_last_character_and_refuses_on_empty()
    {
        TimeEntry entry = new();
        Assert.False(entry.Backspace());
        entry.Type('1');
        entry.Type('2');
        Assert.True(entry.Backspace());
        Assert.Equal("1", entry.Text);
    }

    [Fact]
    public void Every_grid_character_is_accepted_as_itself()
    {
        foreach (char c in TimeEntry.GridCharacters)
        {
            TimeEntry entry = new();
            Assert.True(entry.Type(c));
            Assert.Equal(c.ToString(), entry.Text);
        }
    }

    // Paste and Clear on the keypad screen put a time in the entry; the player still accepts it.
    [Theory]
    [InlineData(0, 0, 0, "0:00.000")]
    [InlineData(1, 23, 456, "1:23.456")]
    [InlineData(59, 59, 999, "59:59.999")]
    public void Set_writes_the_time_as_the_menu_shows_it(int minutes, int seconds, int ms, string expected)
    {
        TimeEntry entry = new();
        entry.Type('9');
        entry.Set(new System.TimeSpan(0, 0, minutes, seconds, ms));
        Assert.Equal(expected, entry.Text);
    }

    [Fact]
    public void A_set_time_reads_back_as_itself()
    {
        TimeEntry entry = new();
        entry.Set(new System.TimeSpan(0, 0, 12, 3, 45));
        Assert.True(TimeParser.TryParseTime(entry.Text, out System.TimeSpan parsed));
        Assert.Equal(new System.TimeSpan(0, 0, 12, 3, 45), parsed);
    }
}
