using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class TimeParser
{
    // The target time is stored as a minutes slider, a seconds slider and three millisecond
    // digits, so nothing downstream can hold an hour or a negative. Parsing one and handing it to
    // the menu set Slider.Index past the end of its own value list.
    public static readonly TimeSpan MaxTargetTime = new(0, 0, 59, 59, 999);

    public static bool TryParseTime(string input, out TimeSpan result)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = TimeSpan.Zero;
            return true;
        }

        if (input.Trim() == "0" || input.Trim() == "00")
        {
            result = TimeSpan.Zero;
            return true;
        }

        string[] timeFormats = [
            @"mm\:ss\.fff", @"m\:ss\.fff",
            @"mm\:ss\.ff",  @"m\:ss\.ff",
            @"mm\:ss\.f",   @"m\:ss\.f",
            @"mm\:ss",      @"m\:ss",
            @"ss\.fff",     @"s\.fff",
            @"ss\.ff",      @"s\.ff",
            @"ss\.f",       @"s\.f",
            @"ss",          @"s",
            @"\.fff",       @"\.ff",       @"\.f"
        ];

        // A comma is the decimal separator on French layouts, where it is unshifted and "." is not.
        string trimmed = input.Trim().Replace(',', '.').TrimStart('0', ':');
        if (string.IsNullOrEmpty(trimmed))
        {
            result = TimeSpan.Zero;
            return true;
        }

        bool success = TimeSpan.TryParseExact(
            trimmed, timeFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);

        // A bare number means seconds, the same unit the `ss` format above gives it. It used to
        // mean milliseconds, which only ever applied past 59 because `ss` swallowed everything
        // below: "59" was 59 seconds and "60" was 60 milliseconds.
        if (!success && int.TryParse(trimmed, System.Globalization.NumberStyles.None,
                                     System.Globalization.CultureInfo.InvariantCulture, out int seconds))
        {
            result = TimeSpan.FromSeconds(seconds);
            success = true;
        }

        if (success && (result < TimeSpan.Zero || result > MaxTargetTime))
        {
            result = TimeSpan.Zero;
            return false;
        }

        return success;
    }
}
