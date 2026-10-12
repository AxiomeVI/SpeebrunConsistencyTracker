#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Several saved layouts in one line of text, each under a key: one set of windows per chapter, in one setting.
///     <c>"Celeste/1-ForsakenCity=1 100 80 600 400;2 700 80 500 300|Celeste/2-OldSite=..."</c>.
/// </summary>
// A layout is whatever text WindowLayout or WidgetPlacement writes; it holds no '|'. A key loses any '|' or
// '=' it has, so any chapter's name can be one. Unreadable entries are skipped rather than failing the rest.
internal static class LayoutBook {
    /// <summary>The layout stored under <paramref name="key"/>, or "" when there is none.</summary>
    public static string Get(string? book, string key) {
        key = Clean(key);
        foreach (string entry in (book ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)) {
            int equals = entry.IndexOf('=');
            if (equals > 0 && entry.AsSpan(0, equals).SequenceEqual(key)) return entry[(equals + 1)..];
        }
        return "";
    }

    /// <summary>The book with <paramref name="layout"/> under <paramref name="key"/>, replacing what was there; "" removes it.</summary>
    public static string Set(string? book, string key, string layout) {
        key = Clean(key);
        layout = layout.Replace("|", "");
        List<string> entries = new();
        foreach (string entry in (book ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)) {
            int equals = entry.IndexOf('=');
            if (equals <= 0 || entry.AsSpan(0, equals).SequenceEqual(key)) continue;
            entries.Add(entry);
        }
        if (layout.Length > 0) entries.Add(key + "=" + layout);
        StringBuilder text = new();
        foreach (string entry in entries) {
            if (text.Length > 0) text.Append('|');
            text.Append(entry);
        }
        return text.ToString();
    }

    private static string Clean(string key) => key.Replace("|", "").Replace("=", "");
}
