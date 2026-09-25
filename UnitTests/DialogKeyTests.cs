using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// Two ways to be wrong, and neither fails anything: a DialogIds constant with no line in
// English.txt renders as "{SCT_WHATEVER}" (Dialog.Clean) with only a warning in log.txt, and a
// line nothing names is dead weight that outlives the feature it belonged to -- SCT_INGAME_OVERLAY
// and the three millisecond-digit labels each survived their own removal.
public class DialogKeyTests
{
    private static string RepoRoot()
    {
        DirectoryInfo dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dialog", "English.txt")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static HashSet<string> KeysInEnglishTxt()
    {
        string path = Path.Combine(RepoRoot(), "Dialog", "English.txt");
        HashSet<string> keys = [];
        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            int eq = trimmed.IndexOf('=');
            if (eq > 0) keys.Add(trimmed[..eq].Trim());
        }
        return keys;
    }

    private static List<string> DialogIdConstants() =>
        [.. typeof(DialogIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)];

    [Fact]
    public void Every_dialog_id_has_a_line_in_English_txt()
    {
        HashSet<string> defined = KeysInEnglishTxt();
        string[] missing = [.. DialogIdConstants().Where(k => !defined.Contains(k)).Order()];

        Assert.Equal([], missing);
    }

    [Fact]
    public void Every_line_in_English_txt_is_named_by_a_dialog_id_or_used_directly()
    {
        // Keys a [SettingName]/[SettingSubText] attribute or Everest itself resolves without going
        // through DialogIds. Each one is here because it is reachable, not because it is exempt.
        HashSet<string> usedElsewhere = [
            "SCT_SPEEBRUN_CONSISTENCY_TRACKER", // Everest's own mod-options title key
        ];

        HashSet<string> referenced = [.. DialogIdConstants()];
        string[] orphans = [.. KeysInEnglishTxt()
            .Where(k => !referenced.Contains(k) && !usedElsewhere.Contains(k))
            .Order()];

        Assert.Equal([], orphans);
    }
}
