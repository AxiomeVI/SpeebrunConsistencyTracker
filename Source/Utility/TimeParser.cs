using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class TimeParser
{
    public static bool TryParseTime(string input, out TimeSpan result)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = TimeSpan.Zero;
            return true;
        }

        // Handle pure zero inputs before any trimming
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

        string trimmed = input.TrimStart('0', ':');
        if (string.IsNullOrEmpty(trimmed))
        {
            result = TimeSpan.Zero;
            return true;
        }

        bool success = TimeSpan.TryParseExact(
            trimmed, timeFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);

        // Fallback: pure number treated as milliseconds
        if (!success && int.TryParse(input, out int msResult))
        {
            result = TimeSpan.FromMilliseconds(msResult);
            success = true;
        }

        return success;
    }
}
