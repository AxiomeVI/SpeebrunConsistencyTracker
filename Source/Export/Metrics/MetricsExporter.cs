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
        private static string lastSessionString = "";

        public static void Reset()
        {
            lastSession = null;
            lastSessionString = "";
        }

        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";
            
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);
            List<string> headers = [.. computedMetrics.Select(res => res.Item1.CsvHeader())];
            headers.Insert(0, "Room/Segment");

            List<string> csvLines = [string.Join(",", headers)];

            List<string> segmentRow = [.. computedMetrics.Select(res => res.Item2.SegmentValue)];
            segmentRow.Insert(0, "Segment");
            csvLines.Add(string.Join(",", segmentRow));

            for (int roomIndex = 0; roomIndex < session.RoomCount; roomIndex++)
            {
                List<string> roomRow = [.. computedMetrics.Select(res => res.Item2.RoomValues.ElementAtOrDefault(roomIndex) ?? "")];
                roomRow.Insert(0, $"R{roomIndex + 1}");
                csvLines.Add(string.Join(",", roomRow));
            }

            return string.Join("\n", csvLines);
        }

        public static bool ExportSessionToOverlay(PracticeSession session, out string result)
        {
            result = "";
            if (session == null || session.TotalCompleted == 0)
                return true;

            if (session.Equals(lastSession) && MetricEngine.SameSettings())
                return false;

            StatTextOrientation orientation = SpeebrunConsistencyTrackerModule.Settings.TextOrientation;

            List<string> overlayString = [];

            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Overlay);

            foreach ((MetricDescriptor desc, MetricResult metricResult) in computedMetrics)
            {
                overlayString.Add($"{desc.InGameName()}" + ": " + $"{metricResult.SegmentValue}");
            }
            string lineSeparator = orientation == StatTextOrientation.Horizontal ? " | " : "\n";
            lastSession = session.DeepClone();
            result = string.Join(lineSeparator, overlayString);
            return true;
        }
    }
}

