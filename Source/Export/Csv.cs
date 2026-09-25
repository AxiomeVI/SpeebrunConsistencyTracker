namespace Celeste.Mod.SpeebrunConsistencyTracker.Export;

public static class Csv
{
    // A value carrying a comma, a quote or a newline silently becomes two columns otherwise.
    // Nothing exported today contains one, but the multimodal summaries are one edit away from it
    // and the failure is invisible in the file.
    public static string Field(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
