#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A line of text with named holes, such as <c>"median {median} · {reps} reps"</c>, filled from values
///     the mod registers by name: a templated text widget a player can word as they like. Parsed once; filled
///     when the values change.
/// </summary>
// The values arrive as text, formatted by the mod: a template never formats a number, so what it shows
// is what the mod's other text shows (the maths boundary). "{{" and "}}" write a brace. A hole whose name
// the mod did not register is left as written and listed in Unknown, so a typo shows on screen rather
// than vanishing; a "{" with no "}" is plain text.
internal sealed class TextTemplate {
    private readonly string[] _literals;   // one more than the holes: text before, between and after them
    private readonly int[] _holes;         // each hole's index into the names

    private TextTemplate(string[] literals, int[] holes, string[] unknown) {
        _literals = literals;
        _holes = holes;
        Unknown = unknown;
    }

    /// <summary>The names in braces the mod did not register, in order of appearance.</summary>
    public IReadOnlyList<string> Unknown { get; }

    /// <summary>How many holes it has.</summary>
    public int HoleCount => _holes.Length;

    /// <param name="names">The names the mod registers; a hole's value is found at the same index in <see cref="Write"/>'s values.</param>
    public static TextTemplate Parse(string? text, IReadOnlyList<string> names) {
        text ??= "";
        List<string> literals = new();
        List<int> holes = new();
        List<string> unknown = new();
        StringBuilder current = new();
        for (int i = 0; i < text.Length; i++) {
            char ch = text[i];
            if ((ch == '{' || ch == '}') && i + 1 < text.Length && text[i + 1] == ch) {
                current.Append(ch);
                i++;
                continue;
            }
            int close = ch == '{' ? text.IndexOf('}', i + 1) : -1;
            if (close < 0) {
                current.Append(ch);
                continue;
            }
            string name = text.Substring(i + 1, close - i - 1).Trim();
            int index = IndexOf(names, name);
            if (index < 0) {
                unknown.Add(name);
                current.Append(text, i, close - i + 1);
            } else {
                literals.Add(current.ToString());
                current.Clear();
                holes.Add(index);
            }
            i = close;
        }
        literals.Add(current.ToString());
        return new TextTemplate(literals.ToArray(), holes.ToArray(), unknown.ToArray());
    }

    /// <summary>The text with every hole filled; a value that is missing or null reads as <paramref name="missing"/>.</summary>
    // Allocates the string: call it when a value changes, not every frame.
    public string Write(IReadOnlyList<string?> values, string missing = "–") {
        StringBuilder text = new(_literals[0]);
        for (int h = 0; h < _holes.Length; h++) {
            int index = _holes[h];
            text.Append(index < values.Count ? values[index] ?? missing : missing);
            text.Append(_literals[h + 1]);
        }
        return text.ToString();
    }

    private static int IndexOf(IReadOnlyList<string> names, string name) {
        for (int i = 0; i < names.Count; i++)
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
