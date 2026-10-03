using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics
{
    public static class MetricsExporter
    {
        private static PracticeSession _lastKnownSession;
        private static uint _lastVersion = 0;
        private static int _lastRoomCount = 0;
        private static int _lastSettingsHash = 0;
        private static int _lastRoomTimerType = 0;

        public static void Clear()
        {
            _lastKnownSession = null;
            _lastVersion = 0;
            _lastRoomCount = 0;
            _lastSettingsHash = 0;
            _lastRoomTimerType = 0;
        }

        public static string ExportSessionToCsv(PracticeSession session, char separator = Csv.FileSeparator)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";

            int segmentLength = SessionManager.RoomCount;
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);

            if (computedMetrics.Count == 0)
                return "";

            List<string> headers = [.. computedMetrics.Select(res => Csv.Field(res.Item1.CsvHeader(), separator))];
            headers.Insert(0, "Room/Segment");

            List<string> csvLines = [string.Join(separator, headers)];

            List<string> segmentRow = [.. computedMetrics.Select(res => Csv.Field(res.Item2.SegmentValue, separator))];
            segmentRow.Insert(0, "Segment");
            csvLines.Add(string.Join(separator, segmentRow));

            for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
            {
                List<string> roomRow = [.. computedMetrics.Select(res => Csv.Field(res.Item2.RoomValues.ElementAtOrDefault(roomIndex), separator))];
                roomRow.Insert(0, $"R{roomIndex + 1}");
                csvLines.Add(string.Join(separator, roomRow));
            }

            return string.Join("\n", csvLines);
        }

        public static bool RefreshTextOverlayIfNecessary(PracticeSession session, out List<string> result)
        {
            result = [];
            int roomCount = SessionManager.RoomCount;

            // A slot switch changes session identity: invalidate everything.
            if (!ReferenceEquals(session, _lastKnownSession))
            {
                _lastKnownSession = session;
                _lastVersion = 0;
                _lastRoomCount = 0;
                _lastSettingsHash = 0;
                _lastRoomTimerType = 0;
            }

            int settingsHash = MetricEngine.GetOverlaySettingsHash();
            int roomTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;
            if (session.Version == _lastVersion && roomCount == _lastRoomCount && settingsHash == _lastSettingsHash && roomTimerType == _lastRoomTimerType)
                return false;

            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Overlay);
            foreach ((MetricDescriptor desc, MetricResult metricResult) in computedMetrics)
                result.Add($"{desc.InGameName()}: {metricResult.SegmentValue}");

            _lastVersion = session.Version;
            _lastRoomCount = roomCount;
            _lastSettingsHash = settingsHash;
            _lastRoomTimerType = roomTimerType;
            return true;
        }
    }
}
