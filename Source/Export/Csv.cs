namespace Celeste.Mod.SpeebrunConsistencyTracker.Export;

public static class Csv
{
    // A value carrying a comma, a quote or a newline silently becomes two columns otherwise.
    // Nothing exported today contains one, but the multimodal summaries are one edit away from it
    // and the failure is invisible in the file.
    //
    // The separator is a comma in a file and a tab on the clipboard: a spreadsheet splits a paste
    // on tabs, and puts a comma-separated one in a single column.
    public const char FileSeparator = ',';
    public const char ClipboardSeparator = '\t';

    public static string Field(string value, char separator = FileSeparator)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny([separator, '"', '\n', '\r']) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
