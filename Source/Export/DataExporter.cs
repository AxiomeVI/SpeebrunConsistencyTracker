using System;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.History;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export;

public static class DataExporter
{
    private static bool TryGetExportData(SessionManager mgr, out PracticeSession session, out int roomCount)
    {
        if (mgr == null || mgr.CurrentSession?.TotalAttempts == 0)
        {
            session = null;
            roomCount = 0;
            return false;
        }
        session = mgr.CurrentSession;
        roomCount = mgr.DynamicRoomCount();
        return true;
    }

    public static void ExportToClipboard(SessionManager mgr)
    {
        if (!TryGetExportData(mgr, out PracticeSession session, out int roomCount))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        StringBuilder sb = new();
        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
        {
            // Clean current clipboard state in case srt export is done in file
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            sb.Append(TextInput.GetClipboardText());
            sb.Append("\n\n\n");
        }
        sb.Append(MetricsExporter.ExportSessionToCsv(session, roomCount));
        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            sb.Append("\n\n\n");
            sb.Append(SessionHistoryCsvExporter.ExportSessionToCsv(session, roomCount));
        }
        TextInput.SetClipboardText(sb.ToString());
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipBoardid));
    }

    public static void ExportToFiles(SessionManager mgr)
    {
        if (!TryGetExportData(mgr, out PracticeSession session, out int roomCount))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
            RoomTimerManager.CmdExportRoomTimes();

        string baseFolder = Path.Combine(
            Everest.PathGame,
            "SCT_Exports",
            SanitizeFileName(session.levelName)
        );
        Directory.CreateDirectory(baseFolder);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_Metrics.csv")))
        {
            writer.WriteLine(MetricsExporter.ExportSessionToCsv(session, roomCount));
        }
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_History.csv")))
        {
            writer.WriteLine(SessionHistoryCsvExporter.ExportSessionToCsv(session, roomCount));
        }

        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileid));
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
