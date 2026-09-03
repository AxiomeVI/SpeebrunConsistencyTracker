using System;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.SessionHistory
{
    public static class SessionHistoryExporter
    {
        public static string ExportSessionToCsv(PracticeSession session)
        {
            if (session.TotalAttempts == 0)
                return "";

            int segmentLength = SessionManager.RoomCount;
            session.RecomputeMaxRoomCount();
            int columnCount = Math.Max(segmentLength, session.MaxRoomCount);
            var sb = new StringBuilder();

            sb.Append("Attempt");
            for (int i = 0; i < columnCount; i++)
                sb.Append($",R{i + 1}");
            sb.Append(",Segment");
            sb.AppendLine();

            var rowCells = new StringBuilder();
            for (int a = 0; a < session.AttemptCount; a++)
            {
                rowCells.Clear();
                bool hasAny = false;
                for (int r = 0; r < columnCount; r++)
                {
                    var cell = session.GetCell(a, r);
                    if (cell.State == RoomCellState.Completed || cell.State == RoomCellState.DNF) hasAny = true;
                    rowCells.Append(cell.HasTime ? $",{cell.Time}" : ",");
                }
                if (!hasAny) continue;

                sb.Append(a + 1);
                sb.Append(rowCells);
                sb.Append(session.IsCompleted(a) ? $",{session.SegmentTime(a)}" : ",");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
