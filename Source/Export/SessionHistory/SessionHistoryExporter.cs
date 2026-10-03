using System;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export.SessionHistory
{
    public static class SessionHistoryExporter
    {
        public static string ExportSessionToCsv(PracticeSession session, char separator = Csv.FileSeparator)
        {
            if (session.TotalAttempts == 0)
                return "";

            // The segment's rooms and no more, as the metrics export does. MaxRoomCount also counts
            // the room after the last one, which a finished run is standing in when the next reset
            // records a DNF there -- so every completed run exported as a death outside the segment.
            int columnCount = session.RoomCount;
            var sb = new StringBuilder();

            sb.Append("Attempt");
            for (int i = 0; i < columnCount; i++)
                sb.Append($"{separator}R{i + 1}");
            sb.Append($"{separator}Segment");
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
                        RoomCellState.Completed => $"{separator}{cell.Time}",
                        RoomCellState.DNF       => $"{separator}DNF",
                        RoomCellState.Deleted   => $"{separator}DEL",
                        _                       => $"{separator}",
                    });
                }
                if (!hasAny) continue;

                sb.Append(a + 1);
                sb.Append(rowCells);
                sb.Append(session.IsCompleted(a) ? $"{separator}{session.SegmentTime(a)}" : $"{separator}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
