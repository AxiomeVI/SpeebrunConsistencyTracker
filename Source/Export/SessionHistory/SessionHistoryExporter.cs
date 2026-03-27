using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.History
{
    public static class SessionHistoryExporter
    {
        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session.TotalAttempts == 0)
                return "";

            int segmentLength = SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
            var sb = new StringBuilder();

            sb.Append("Attempt");
            for (int i = 0; i < segmentLength; i++)
                sb.Append($",R{i + 1}");
            sb.Append(",Segment");
            sb.AppendLine();

            foreach ((Attempt attempt, int i) in session.Attempts.Select((attempt, i) => (attempt, i + 1)))
            {
                sb.Append(i);
                for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
                    sb.Append(roomIndex < attempt.Count ? $",{attempt.CompletedRooms[roomIndex]}" : ",");
                sb.Append(attempt.IsCompleted() ? $",{attempt.SegmentTime()}" : ",");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static IList<IList<object>> ExportSessionToSheet(PracticeSession session)
        {
            if (session.TotalAttempts == 0)
                return [];

            int segmentLength = SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
            IList<IList<object>> rows = [];

            List<object> header = ["Attempt"];
            for (int i = 0; i < segmentLength; i++)
                header.Add($"R{i + 1}");
            header.Add("Segment");
            rows.Add(header);

            foreach ((Attempt attempt, int i) in session.Attempts.Select((attempt, i) => (attempt, i + 1)))
            {
                List<object> row = [i];
                for (int roomIndex = 0; roomIndex < segmentLength; roomIndex++)
                    row.Add(roomIndex < attempt.Count ? attempt.CompletedRooms[roomIndex].ToString() : "");
                row.Add(attempt.IsCompleted() ? attempt.SegmentTime().ToString() : "");
                rows.Add(row);
            }

            return rows;
        }
    }
}
