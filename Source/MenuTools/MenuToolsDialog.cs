// MenuTools requires: nothing else
namespace Celeste.Mod.MenuTools;

/// <summary>
/// Text the library shows to players, looked up in the mod's dialog files. <c>tools/vendor.sh</c> writes the keys
/// into the mod's <c>Dialog/&lt;Language&gt;.txt</c> from the library's <c>dialog/</c> folder.
/// </summary>
internal static class MenuToolsDialog {
    // tools/vendor.sh replaces this with a prefix of the mod's own, so two mods' copies never share a dialog key.
    // Keep it on one line.
    internal const string Prefix = "SPEEBRUNCONSISTENCYTRACKER_MENUTOOLS_";

    /// <summary>
    /// The text of <see cref="Prefix"/> + <paramref name="key"/> in the current language, or
    /// <paramref name="english"/> if no dialog file has it
    /// </summary>
    /// <remarks>
    /// Keep each call on one line, with literal arguments: test/build-everest-versions.sh reads them to check that
    /// <paramref name="english"/> matches dialog/English.txt.
    /// </remarks>
    internal static string Get(string key, string english) {
        string fullKey = Prefix + key;
        return Dialog.Has(fullKey) ? Dialog.Clean(fullKey) : english;
    }
}
