namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

/// <summary>What the target-time entry screen has typed so far.</summary>
public sealed class TimeEntry
{
    /// <summary>"mm:ss.fff", the longest time the parser accepts.</summary>
    public const int MaxLength = 9;

    /// <summary>The characters the on-screen grid offers, three to a row like a phone keypad.</summary>
    public const string GridCharacters = "123456789:0.";

    // AZERTY's unshifted number row types these instead of digits, French layout first. Belgian
    // AZERTY differs on 6 and 8. In a time none of them could mean anything else.
    private const string AzertyNumberRow = "&é\"'(-è_çà";
    private const string Digits          = "1234567890";

    public string Text { get; private set; } = "";

    /// <summary>The character <paramref name="c"/> stands for in a time, or null.</summary>
    public static char? Normalize(char c)
    {
        if (c is >= '0' and <= '9' or ':') return c;
        // "," is the French decimal separator, and ";" is the unshifted key under "." on AZERTY.
        if (c is '.' or ',' or ';') return '.';
        int i = AzertyNumberRow.IndexOf(c);
        if (i >= 0) return Digits[i];
        return c switch { '§' => '6', '!' => '8', _ => null };
    }

    /// <returns>False when the character is refused or the entry is full.</returns>
    public bool Type(char c)
    {
        if (Normalize(c) is not char n || Text.Length >= MaxLength) return false;
        Text += n;
        return true;
    }

    /// <summary>Replaces the text with <paramref name="time"/> as m:ss.fff, the way the menu shows it.</summary>
    public void Set(System.TimeSpan time)
    {
        Text = "";
        foreach (char c in $"{(int)time.TotalMinutes}:{time.Seconds:D2}.{time.Milliseconds:D3}") Type(c);
    }

    /// <returns>False when there was nothing to remove.</returns>
    public bool Backspace()
    {
        if (Text.Length == 0) return false;
        Text = Text[..^1];
        return true;
    }
}
