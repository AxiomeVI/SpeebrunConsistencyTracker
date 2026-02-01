using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics
{
    public static class MetricsExporter
    {

        private static PracticeSession lastSession = null;
        private static string lastSessionString = "";

        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session == null || session.TotalAttempts == 0)
                return "";
            
            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Export);
            List<string> headers = computedMetrics.Select(res => res.Item1.CsvHeader()).ToList();
            headers.Insert(0, "Room/Segment");

            List<string> csvLines = new List<string> { string.Join(",", headers) };

            List<string> segmentRow = computedMetrics.Select(res => res.Item2.SegmentValue).ToList();
            segmentRow.Insert(0, "Segment");
            csvLines.Add(string.Join(",", segmentRow));

            for (int roomIndex = 0; roomIndex < session.RoomCount; roomIndex++)
            {
                List<string> roomRow = computedMetrics.Select(res => res.Item2.RoomValues[roomIndex]).ToList();
                roomRow.Insert(0, $"R{roomIndex+1}");
                csvLines.Add(string.Join(",", roomRow));
            }

            return string.Join("\n", csvLines);
        }

        public static string ExportSessionToOverlay(PracticeSession session)
        {
            if (session == null || session.TotalCompleted == 0)
                return "";

            if (session.Equals(lastSession) && MetricEngine.SameSettings())
                return lastSessionString;

            StatTextOrientation orientation = SpeebrunConsistencyTrackerModule.Settings.TextOrientation;

            List<string> overlayString = new List<string>();

            List<(MetricDescriptor, MetricResult)> computedMetrics = MetricEngine.Compute(session, MetricOutput.Overlay);

            foreach ((MetricDescriptor desc, MetricResult result) in computedMetrics)
            {
                overlayString.Add($"{desc.InGameName()}" + ": " + $"{result.SegmentValue}");
            }
            string lineSeparator = orientation == StatTextOrientation.Horizontal ? " | " : "\n";
            lastSession = session;
            lastSessionString = string.Join(lineSeparator, overlayString);
            return lastSessionString;
        }
    }
}

