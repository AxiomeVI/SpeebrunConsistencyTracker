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
        if (SessionManager.CurrentSession?.TotalAttempts == 0)
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

        StringBuilder sb = new();
        _ = sb.Append(MetricsExporter.ExportSessionToCsv(session));
        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
        {
            _ = sb.Append("\n\n\n");
            // The SRT export may go to a file, leaving a stale clipboard behind.
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            _ = sb.Append(TextInput.GetClipboardText());
        }
        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            _ = sb.Append("\n\n\n");
            _ = sb.Append(SessionHistoryExporter.ExportSessionToCsv(session));
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

        string baseFolder = Path.Combine(
            Everest.PathGame,
            "SCT_Exports",
            SanitizeFileName(SessionManager.LevelName)
        );
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        try
        {
            _ = Directory.CreateDirectory(baseFolder);
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

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
        var sanitized = new string(
            [.. input.Where(ch => !invalidChars.Contains(ch))]
        );
        return sanitized.TrimEnd(' ', '.');
    }
}
