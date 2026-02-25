using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.History
{
    public static class SessionHistoryCsvExporter
    {
        public static string ExportSessionToCsv(PracticeSession session, int segmentLength)
        {
            if (session.TotalAttempts == 0)
                return "";

            var sb = new StringBuilder();

            // Header
            sb.Append("Attempt");
            for (int i = 0; i < segmentLength; i++)
                sb.Append($",R{i + 1}");
            sb.Append(",Segment");
            sb.AppendLine();

            // Rows
            foreach ((Attempt attempt, int i) in session.Attempts.Select((attempt, i) => (attempt, i + 1)))
            {
                sb.Append(i);

                for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
                {
                    if (roomIndex >= 0 && roomIndex < attempt.Count)
                    {
                        sb.Append($",{attempt.CompletedRooms[roomIndex]}");
                    }
                    else
                    {
                        sb.Append(',');
                    }
                }

                sb.Append(attempt.IsCompleted(segmentLength) ? $",{attempt.SegmentTime(segmentLength)}" : ",");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
