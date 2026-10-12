namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class LevelNames
{
    // The whole SID, not the part after the first '-': a SID may have none ("Beginner/zoey"), and
    // dropping its '/' would merge sibling maps into one name. The side is part of the identity
    // too, or A and B share a folder.
    public static string ForExportFolder(string sid, int mode)
    {
        if (string.IsNullOrWhiteSpace(sid)) return "unknown";
        string name = sid.Replace('/', '_').Replace('\\', '_').Trim('_');
        if (string.IsNullOrEmpty(name)) return "unknown";
        return mode switch
        {
            1 => name + "_B",
            2 => name + "_C",
            _ => name,
        };
    }
}
