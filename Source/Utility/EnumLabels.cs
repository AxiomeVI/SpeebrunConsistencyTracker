using System;
using System.Globalization;
using System.Text;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class EnumLabels
{
    // Enum members reached the menus as raw identifiers -- "MiddleCenter", "MadelineRed", "P90".
    // A dialog key wins where one exists, so a translator can name them; otherwise the member name
    // is split on its own capitals, which reads correctly for every member this mod has and never
    // shows the "{SCT_...}" a missing key would.
    public static string For<T>(T value) where T : struct, Enum
        => Label(typeof(T).Name, value.ToString());

    internal static string Label(string enumName, string memberName)
    {
        string key = string.Concat("SCT_ENUM_", enumName, "_", memberName).ToUpperInvariant();
        return Dialog.Has(key) ? Dialog.Clean(key) : Humanize(memberName);
    }

    internal static string Humanize(string memberName)
    {
        if (string.IsNullOrEmpty(memberName)) return memberName;
        StringBuilder sb = new(memberName.Length + 4);
        for (int i = 0; i < memberName.Length; i++)
        {
            char c = memberName[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(memberName[i - 1]))
            {
                _ = sb.Append(' ').Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }
            else
            {
                _ = sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
