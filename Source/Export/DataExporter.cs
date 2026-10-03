using System;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.SessionHistory;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export;

public static class DataExporter
{

    private static bool TryGetExportData(out PracticeSession session)
    {
        // `?.TotalAttempts == 0` was false for a null session, so exporting from the pause menu
        // before any attempt existed wrote an empty file, or threw inside the history exporter.
        if (SessionManager.CurrentSession is not { TotalAttempts: > 0 })
        {
            session = null;
            return false;
        }
        SessionManager.UpdateRoomCount();
        session = SessionManager.CurrentSession;
        return true;
    }

    public static void ExportToClipboard()
    {
        if (!TryGetExportData(out PracticeSession session))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportId));
            return;
        }

        const char separator = Csv.ClipboardSeparator;
        StringBuilder sb = new();
        _ = sb.Append(MetricsExporter.ExportSessionToCsv(session, separator));
        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
        {
            _ = sb.Append("\n\n\n");
            // The SRT export may go to a file, leaving a stale clipboard behind.
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            // Speedrun Tool writes commas and never quotes: its fields are room numbers and times.
            _ = sb.Append(TextInput.GetClipboardText()?.Replace(Csv.FileSeparator, separator));
        }
        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            _ = sb.Append("\n\n\n");
            _ = sb.Append(SessionHistoryExporter.ExportSessionToCsv(session, separator));
        }
        TextInput.SetClipboardText(sb.ToString());
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipboardId));
    }

    public static void ExportToFiles()
    {
        if (!TryGetExportData(out PracticeSession session))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportId));
            return;
        }

        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
            RoomTimerManager.CmdExportRoomTimes();

        // PathEverest, not PathGame: they are the same folder in a normal install, but in XDG mode
        // (an EverestXDGFlag file, for read-only game folders) Everest moves Mods and its settings
        // to a writable ~/.local/share/Everest while PathGame stays where writing may fail.
        string baseFolder = Path.Combine(
            Everest.PathEverest,
            "SCT_Exports",
            SanitizeFileName(SessionManager.LevelName)
        );
        try
        {
            _ = Directory.CreateDirectory(baseFolder);
            // Second resolution alone let two exports in the same second overwrite each other.
            string timestamp = UniqueTimestamp(baseFolder);
            using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_Metrics.csv")))
            {
                writer.WriteLine(MetricsExporter.ExportSessionToCsv(session));
            }
            if (SpeebrunConsistencyTrackerModule.Settings.History)
            {
                using StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_History.csv"));
                writer.WriteLine(SessionHistoryExporter.ExportSessionToCsv(session));
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker), $"File export failed: {ex.Message}");
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileFailedId));
            return;
        }

        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileId));
    }

    // Suffixed only on collision, so the common case keeps the plain readable timestamp.
    internal static string UniqueTimestamp(string folder, Func<string, bool> exists = null)
    {
        exists ??= File.Exists;
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (!exists(Path.Combine(folder, $"{stamp}_Metrics.csv"))) return stamp;
        for (int n = 2; n < 1000; n++)
        {
            string candidate = $"{stamp}_{n}";
            if (!exists(Path.Combine(folder, $"{candidate}_Metrics.csv"))) return candidate;
        }
        return stamp;
    }

    private const string WindowsReserved = "<>:\"/\\|?*";

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        // Windows' reserved set is named outright, on top of whatever the running platform
        // reports. Path.GetInvalidFileNameChars() is platform-dependent -- on Linux it is '\0'
        // and '/' and nothing else -- so a SID carrying a ':' or a '\\' sanitised cleanly there
        // and produced an unwritable path on the machine most players are on.
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars())
            .Concat(WindowsReserved)
            .Distinct().ToArray();
        var sanitized = new string(
            [.. input.Where(ch => !invalidChars.Contains(ch))]
        );
        return sanitized.TrimEnd(' ', '.');
    }
}
