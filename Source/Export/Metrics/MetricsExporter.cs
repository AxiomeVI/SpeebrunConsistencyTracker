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
        private static int lastSegmentLength = 0;

        public static void Clear()
        {
            lastSession = null;
            lastSegmentLength = 0;
        }

        public static string ExportSessionToCsv(PracticeSession session, int segmentLength)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";
            
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, segmentLength, MetricOutput.Export);
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

        public static bool TryExportSessionToOverlay(PracticeSession session, int segmentLength, out List<string> result)
        {
            result = [];
            if (session == null || session.TotalCompleted(segmentLength) == 0)
            {
                lastSegmentLength = segmentLength;
                return true;
            }

            if (session.Equals(lastSession) && lastSegmentLength == segmentLength && MetricEngine.SameSettings())
            {
                return false;
            }

            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, segmentLength, MetricOutput.Overlay);
            foreach ((MetricDescriptor desc, MetricResult metricResult) in computedMetrics)
            {
                result.Add($"{desc.InGameName()}" + ": " + $"{metricResult.SegmentValue}");
            }
            lastSession = session.DeepClone();
            lastSegmentLength = segmentLength;
            return true;
        }
    }
}

