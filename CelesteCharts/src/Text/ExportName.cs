#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A file name for an exported chart: its title and when, safe on every system the game runs on.</summary>
internal static class ExportName {
    /// <summary><c>"Frames lost 2026-09-30 21-14-05.png"</c>: the title with characters no file system takes replaced.</summary>
    public static string For(string title, DateTime time, string extension = ".png") {
        StringBuilder name = new();
        foreach (char c in title) name.Append(Array.IndexOf(Invalid, c) >= 0 || char.IsControl(c) ? '-' : c);
        string clean = name.ToString().Trim().Trim('.');
        if (clean.Length > 80) clean = clean[..80].TrimEnd();
        if (clean.Length == 0) clean = "chart";
        return clean + " " + time.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) + extension;
    }

    // Windows' list, which is the strictest; Linux and macOS refuse only '/' and NUL, both in it.
    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'];
}
