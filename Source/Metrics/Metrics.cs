using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static partial class Metrics
    {
        public static MetricResult Average(PracticeSession session, MetricContext context, bool isExport)
        {
            // Compute segment average
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (!segmentTimes.Any())
            {
                segmentValue = "0";
            }
            else
            {
                double avg = context.GetOrCompute("avg_segment", () =>
                    segmentTimes.Average(t => t.Ticks));

                segmentValue = new TimeTicks((long)Math.Round(avg)).ToString();
            }

            // Compute per-room averages
            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = session.GetRoomTimes(r).ToList();
                    if (!roomTimes.Any())
                    {
                        roomValues.Add("0");
                    }
                    else
                    {
                        double roomAvg = context.GetOrCompute($"avg_room_{r}", () =>
                            roomTimes.Average(t => t.Ticks));

                        roomValues.Add(new TimeTicks((long)Math.Round(roomAvg)).ToString());
                    }
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Median(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue;

            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                             .OrderBy(t => t)
                             .ToList()
            );

            segmentValue = MetricHelper.ComputePercentile(segmentValues, 50).ToString();

            int roomCount = session.RoomCount;
            List<string> RoomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    string roomKey = $"room_{roomIndex}_values_sorted";

                    var roomValues = context.GetOrCompute(
                        roomKey,
                        () => session.GetRoomTimes(roomIndex)
                                    .OrderBy(t => t)
                                    .ToList()
                    );

                    RoomValues.Add(MetricHelper.ComputePercentile(roomValues, 50).ToString());
                }
            }

            return new MetricResult(segmentValue, RoomValues);
        }

        public static MetricResult StdDev(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (segmentTimes.Count < 2)
            {
                segmentValue = "0";
                context.Set("std_segment", 0.0);
            }
            else
            {
                // Reuse average if available, otherwise compute
                double avg = context.GetOrCompute("avg_segment", () =>
                    segmentTimes.Average(t => t.Ticks));

                double variance = segmentTimes.Sum(t => Math.Pow(t.Ticks - avg, 2)) / (segmentTimes.Count - 1);
                double stdDouble = Math.Sqrt(variance);

                context.Set("std_segment", stdDouble); // store segment std in context
                segmentValue = new TimeTicks((long)Math.Round(stdDouble)).ToString();
            }

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = session.GetRoomTimes(r).ToList();
                    if (roomTimes.Count < 2)
                    {
                        roomValues.Add("0");
                        context.Set($"std_room_{r}", 0.0);
                    }
                    else
                    {
                        double avgRoom = context.GetOrCompute($"avg_room_{r}", () =>
                            roomTimes.Average(t => t.Ticks));

                        double variance = roomTimes.Sum(t => Math.Pow(t.Ticks - avgRoom, 2)) / (roomTimes.Count - 1);
                        double stdRoom = Math.Sqrt(variance);

                        context.Set($"std_room_{r}", stdRoom); // store room std in context
                        roomValues.Add(new TimeTicks((long)Math.Round(stdRoom)).ToString());
                    }
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult CoefVariation(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (segmentTimes.Count < 2)
            {
                segmentValue = "0";
            }
            else
            {
                double avg = context.GetOrCompute("avg_segment", () =>
                    segmentTimes.Average(t => t.Ticks));

                double std = context.GetOrCompute("std_segment", () =>
                {
                    double variance = segmentTimes.Sum(v => Math.Pow(v.Ticks - avg, 2)) / (segmentTimes.Count - 1);
                    return Math.Sqrt(variance);
                });

                double cv = avg == 0.0 ? 0.0 : std / avg;
                segmentValue = MetricHelper.FormatPercent(cv);
            }

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = session.GetRoomTimes(r).ToList();
                    if (roomTimes.Count < 2)
                    {
                        roomValues.Add("0");
                    }
                    else
                    {
                        double avgRoom = context.GetOrCompute($"avg_room_{r}", () =>
                            roomTimes.Average(t => t.Ticks));

                        double stdRoom = context.GetOrCompute($"std_room_{r}", () =>
                        {
                            double variance = segmentTimes.Sum(v => Math.Pow(v.Ticks - avgRoom, 2)) / (segmentTimes.Count - 1);
                            return Math.Sqrt(variance);
                        });
                        double cv = avgRoom == 0.0 ? 0.0 : stdRoom / avgRoom;
                        roomValues.Add(MetricHelper.FormatPercent(cv));
                    }
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Best(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                            .OrderBy(t => t)
                            .ToList()
            );

            string segmentValue;
            if (segmentSorted.Count == 0)
            {
                segmentValue = "0";
                context.Set("min_segment", TimeTicks.Zero);
            }
            else
            {
                TimeTicks best = segmentSorted[0];
                segmentValue = best.ToString();
                context.Set("min_segment", best);
            }

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);

            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomSorted = context.GetOrCompute(
                        $"room_{r}_values_sorted",
                        () => session.GetRoomTimes(r)
                                    .OrderBy(t => t)
                                    .ToList()
                    );

                    if (roomSorted.Count == 0)
                    {
                        roomValues.Add("0");
                        context.Set($"min_room_{r}", TimeTicks.Zero);
                    }
                    else
                    {
                        TimeTicks best = roomSorted[0];
                        roomValues.Add(best.ToString());
                        context.Set($"min_room_{r}", best);
                    }
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Worst(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                            .OrderBy(t => t)
                            .ToList()
            );

            string segmentValue =
                segmentSorted.Count == 0
                    ? "0"
                    : segmentSorted[^1].ToString();

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomSorted = context.GetOrCompute(
                        $"room_{r}_values_sorted",
                        () => session.GetRoomTimes(r)
                                    .OrderBy(t => t)
                                    .ToList()
                    );

                    roomValues.Add(
                        roomSorted.Count == 0
                            ? "0"
                            : roomSorted[^1].ToString()
                    );
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult SumOfBest(PracticeSession session, MetricContext context)
        {
            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            long sumTicks = 0;

            for (int r = 0; r < roomCount; r++)
            {
                // Retrieve best per-room TimeTicks from cache
                if (context.TryGet<TimeTicks>($"min_room_{r}", out var bestRoom))
                {
                    sumTicks += bestRoom.Ticks;
                    roomValues.Add(new TimeTicks(sumTicks).ToString());
                }
                else
                {
                    // fallback if Best metric not computed
                    var roomTimes = session.GetRoomTimes(r).ToList();
                    var minRoom = roomTimes.Any() ? roomTimes.Min() : TimeTicks.Zero;
                    sumTicks += minRoom.Ticks;
                    roomValues.Add(new TimeTicks(sumTicks).ToString());
                }
            }

            var segmentValue = new TimeTicks(sumTicks).ToString();
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult SuccessRate(PracticeSession session, MetricContext context)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            TimeTicks targetTime = MetricEngine.GetTargetTimeTicks();

            if (!segmentTimes.Any())
            {
                return new MetricResult("", []);
            }

            int successCount = segmentTimes.Select(s => s <= targetTime).Count();
            double successRate = (double)successCount / session.TotalCompleted;

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                roomValues.Add("");
            }

            return new MetricResult(MetricHelper.FormatPercent(successRate), roomValues);
        }
        
        public static MetricResult Percentile(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue;
            int percentile = MetricHelper.ToInt(SpeebrunConsistencyTrackerModule.Settings.StatsMenu.PercentileValue);

            // ---------- Segment ----------
            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                             .OrderBy(t => t)
                             .ToList()
            );

            segmentValue = MetricHelper.ComputePercentile(segmentSorted, percentile).ToString();

            int roomCount = session.RoomCount;
            List<string> RoomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomSorted = context.GetOrCompute(
                        $"room_{r}_values_sorted",
                        () => session.GetRoomTimes(r)
                                    .OrderBy(t => t)
                                    .ToList()
                    );

                    RoomValues.Add(MetricHelper.ComputePercentile(roomSorted, percentile).ToString());
                }
            }

            return new MetricResult(segmentValue, RoomValues);
        }

        public static MetricResult CompletedRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue = session.TotalCompleted.ToString();
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    roomValues.Add(session.CompletedRunsPerRoom.GetValueOrDefault((RoomIndex)index).ToString());
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult TotalRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue = session.TotalAttempts.ToString();
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    roomValues.Add(session.TotalAttemptsPerRoom.GetValueOrDefault((RoomIndex)index).ToString());
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult DnfCount(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue = session.TotalDnfs.ToString();
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    roomValues.Add(session.DnfPerRoom.GetValueOrDefault((RoomIndex)index).ToString());
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ResetRate(PracticeSession session, MetricContext context, bool isExport)
        {
            int dnfCount = session.TotalDnfs;
            int runCount = session.TotalAttempts;
            string segmentValue = runCount == 0 ? "" : MetricHelper.FormatPercent((double)dnfCount / runCount);
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomDnfCount = session.DnfPerRoom.GetValueOrDefault((RoomIndex)index);
                    int roomRunCount = session.TotalAttemptsPerRoom.GetValueOrDefault((RoomIndex)index);
                    roomValues.Add(roomRunCount == 0 ? "" : MetricHelper.FormatPercent((double)roomDnfCount / roomRunCount));
                }
            }
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ResetShare(PracticeSession session, MetricContext context, bool isExport)
        {   
            if (!isExport)
                return new MetricResult("", []);

            int dnfCount = session.TotalDnfs;
            string segmentValue = dnfCount == 0 ? "0%" : "100%";
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomDnfCount = session.DnfPerRoom.GetValueOrDefault((RoomIndex)index);
                    roomValues.Add(dnfCount == 0 ? "0%" : MetricHelper.FormatPercent((double)roomDnfCount / dnfCount));
                }
            }
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult TrendSlope(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue = MetricHelper.LinearRegression(segmentTimes).ToString();


            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = session.GetRoomTimes(r).ToList();
                    roomValues.Add(MetricHelper.LinearRegression(roomTimes).ToString());
                }
            }

            return new MetricResult(segmentValue, roomValues);
        }

        /*
        public bool LinearRegression { get; set; } = true;
        */
    }
}
