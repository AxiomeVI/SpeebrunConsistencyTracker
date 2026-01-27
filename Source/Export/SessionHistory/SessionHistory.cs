using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.History
{
    public static class SessionHistoryCsvExporter
    {
        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session.Attempts.Count == 0)
                return string.Empty;

            int roomCount = session.RoomCount;

            var sb = new StringBuilder();

            // Header
            sb.Append("Attempt");
            for (int i = 0; i < roomCount; i++)
                sb.Append($",R{i + 1}");
            sb.Append(",Segment");
            sb.AppendLine();

            // Rows
            foreach (var attempt in session.Attempts)
            {
                sb.Append(attempt.Index + 1);

                TimeTicks segmentTotal = TimeTicks.Zero;

                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    if (attempt.CompletedRooms.TryGetValue(new RoomIndex(roomIndex), out TimeTicks roomTime))
                    {
                        sb.Append($",{roomTime}");
                        if (attempt.IsCompleted)
                            segmentTotal += roomTime;
                    }
                    else
                    {
                        sb.Append(",");
                    }
                }

                sb.Append(attempt.IsCompleted ? $",{segmentTotal}" : ",");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
