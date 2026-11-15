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
        private static MetricEngine.OverlayKey _lastKey;
        private static int _lastRoomTimerType = 0;

        public static void Clear()
        {
            _lastKnownSession = null;
            _lastVersion = 0;
            _lastRoomCount = 0;
            _lastKey = default;
            _lastRoomTimerType = 0;
        }

        public static string ExportSessionToCsv(PracticeSession session, char separator = Csv.FileSeparator)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";

            int segmentLength = SessionManager.RoomCount;
            List<MetricEngine.ComputedMetric> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);

            if (computedMetrics.Count == 0)
                return "";

            List<string> headers = [.. computedMetrics.Select(res => Csv.Field(res.CsvHeader, separator))];
            headers.Insert(0, "Room/Segment");

            List<string> csvLines = [string.Join(separator, headers)];

            List<string> segmentRow = [.. computedMetrics.Select(res => Csv.Field(res.Result.SegmentValue, separator))];
            segmentRow.Insert(0, "Segment");
            csvLines.Add(string.Join(separator, segmentRow));

            for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
            {
                List<string> roomRow = [.. computedMetrics.Select(res => Csv.Field(res.Result.RoomValues.ElementAtOrDefault(roomIndex), separator))];
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
                _lastKey = default;
                _lastRoomTimerType = 0;
            }

            MetricEngine.OverlayKey key = MetricEngine.GetOverlayKey();
            int roomTimerType = (int)Integration.Srt.Tool.TimerType;
            if (session.Version == _lastVersion && roomCount == _lastRoomCount && key == _lastKey && roomTimerType == _lastRoomTimerType)
                return false;

            foreach (MetricEngine.ComputedMetric metric in MetricEngine.Compute(session, MetricOutput.Overlay))
                result.Add($"{metric.OverlayName}: {metric.Result.SegmentValue}");

            _lastVersion = session.Version;
            _lastRoomCount = roomCount;
            _lastKey = key;
            _lastRoomTimerType = roomTimerType;
            return true;
        }
    }
}
