using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System.Linq;
using Force.DeepCloner;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics
{
    public static class MetricsExporter
    {
        private static PracticeSession lastSession = null;
        private static int lastRoomCount = 0;

        public static void Clear()
        {
            lastSession = null;
            lastRoomCount = 0;
        }

        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";

            int segmentLength = SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);

            if (computedMetrics.Count == 0)
                return "";

            List<string> headers = [.. computedMetrics.Select(res => res.Item1.CsvHeader())];
            headers.Insert(0, "Room/Segment");

            List<string> csvLines = [string.Join(",", headers)];

            List<string> segmentRow = [.. computedMetrics.Select(res => res.Item2.SegmentValue)];
            segmentRow.Insert(0, "Segment");
            csvLines.Add(string.Join(",", segmentRow));

            for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
            {
                List<string> roomRow = [.. computedMetrics.Select(res => res.Item2.RoomValues.ElementAtOrDefault(roomIndex) ?? "")];
                roomRow.Insert(0, $"R{roomIndex + 1}");
                csvLines.Add(string.Join(",", roomRow));
            }

            return string.Join("\n", csvLines);
        }

        public static IList<IList<object>> ExportMetricsToSheet(PracticeSession session)
        {
            if (session == null || session.TotalAttempts == 0)
                return [];

            int segmentLength = SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);

            if (computedMetrics.Count == 0)
                return [];

            IList<IList<object>> rows = [];

            List<object> headers = [.. computedMetrics.Select(res => (object)res.Item1.CsvHeader())];
            headers.Insert(0, "Room/Segment");
            rows.Add(headers);

            List<object> segmentRow = [.. computedMetrics.Select(res => (object)res.Item2.SegmentValue)];
            segmentRow.Insert(0, "Segment");
            rows.Add(segmentRow);

            for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
            {
                List<object> roomRow = [.. computedMetrics.Select(res => (object)(res.Item2.RoomValues.ElementAtOrDefault(roomIndex) ?? ""))];
                roomRow.Insert(0, $"R{roomIndex + 1}");
                rows.Add(roomRow);
            }

            return rows;
        }

        public static bool TryExportSessionToOverlay(PracticeSession session, out List<string> result)
        {
            result = [];
            int roomCount = SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
            if (session == null || session.TotalCompleted() == 0)
            {
                lastRoomCount = roomCount;
                return true;
            }

            if (session.Equals(lastSession) && lastRoomCount == roomCount && MetricEngine.SameSettings())
                return false;

            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Overlay);
            foreach ((MetricDescriptor desc, MetricResult metricResult) in computedMetrics)
                result.Add($"{desc.InGameName()}: {metricResult.SegmentValue}");

            lastSession = session.DeepClone();
            lastRoomCount = roomCount;
            return true;
        }
    }
}
