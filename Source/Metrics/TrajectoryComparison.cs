namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // One room of a pinned line read against a second line and against the best split: the numbers
    // the trajectory's table and its room card both show. A null line is the reference the chart
    // measures from (the average run, or the sum of bests), whose deviation is 0 everywhere.
    //
    // Every delta is signed as time: negative is ahead.
    internal readonly record struct TrajectoryComparison(
        long MainRoom,          // the pinned line's time in the room
        long MainRunning,       // and its time at the end of it
        bool HasBest,           // another run reached the room
        long VsBest,            // the gap, at the end of the room, to the best any other run had there
        bool CompReaches,       // the second line reaches the room
        long CompRoom,
        long CompRunning,
        long CumulDelta,        // the gap to the second line at the end of the room
        bool RoomKnown,         // the second line has a time in the room
        long RoomDelta)         // the gap in this room alone
    {
        public static TrajectoryComparison At(TrajectoryModel model, AttemptLine main, AttemptLine comp, int[] bestAmongOthers, int room)
        {
            long mainRoom  = TimeOf(model, main, room);
            long mainCumul = main == null ? 0 : main.CumulativeDeviations[room];

            int best = bestAmongOthers.Length > room ? bestAmongOthers[room] : -1;
            long vsBest = best >= 0 ? mainCumul - model.Attempts[best].CumulativeDeviations[room] : 0;

            // Past the end of the second line there is nothing to compare against.
            bool reaches   = comp == null || room < comp.RoomsCompleted;
            long compRoom  = reaches ? TimeOf(model, comp, room) : 0;
            long compCumul = comp == null || !reaches ? 0 : comp.CumulativeDeviations[room];
            bool roomKnown = comp == null || compRoom > 0;

            return new TrajectoryComparison(
                mainRoom, Running(model, main, room), best >= 0, vsBest,
                reaches, compRoom, reaches ? Running(model, comp, room) : 0, mainCumul - compCumul,
                roomKnown, mainRoom - compRoom);
        }

        private static long TimeOf(TrajectoryModel model, AttemptLine line, int room) =>
            line == null ? model.RoomReference[room] : line.RoomTimes[room];

        private static long Running(TrajectoryModel model, AttemptLine line, int room)
        {
            long sum = 0;
            for (int i = 0; i <= room; i++) sum += TimeOf(model, line, i);
            return sum;
        }
    }
}
