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
            // Read, not recomputed: SessionManager.UpdateRoomCount owns MaxRoomCount and RoomCount
            // is derived from it, so an export has no business moving either.
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
                    // A DNF and a deleted cell used to export as empty, exactly like a room the
                    // run never reached, so the file did not say where any run died.
                    rowCells.Append(cell.State switch
                    {
                        RoomCellState.Completed => $",{cell.Time}",
                        RoomCellState.DNF       => ",DNF",
                        RoomCellState.Deleted   => ",DEL",
                        _                       => ",",
                    });
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
