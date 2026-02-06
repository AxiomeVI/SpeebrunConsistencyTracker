using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.History
{
    public static class SessionHistoryCsvExporter
    {
        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session.TotalAttempts == 0)
                return "";

            int roomCount = session.RoomCount;

            var sb = new StringBuilder();

            // Header
            sb.Append("Attempt");
            for (int i = 0; i < roomCount; i++)
                sb.Append($",R{i + 1}");
            sb.Append(",Segment");
            sb.AppendLine();

            // Rows
            foreach (var (attempt, i) in session.Attempts.Select((attempt, i) => (attempt, i + 1)))
            {
                sb.Append(i);

                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    if (attempt.CompletedRooms.TryGetValue(new RoomIndex(roomIndex), out TimeTicks roomTime))
                    {
                        sb.Append($",{roomTime}");
                    }
                    else
                    {
                        sb.Append(",");
                    }
                }

                sb.Append(attempt.IsCompleted ? $",{attempt.SegmentTime}" : ",");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
