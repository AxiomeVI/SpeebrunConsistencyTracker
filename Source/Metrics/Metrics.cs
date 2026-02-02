using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
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

            if (segmentTimes.Count == 0)
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
                    if (roomTimes.Count == 0)
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

            var median = context.GetOrCompute(
                "med_segment",
                () => MetricHelper.ComputePercentile(segmentValues, 50)
            );

            segmentValue = median.ToString();

            int roomCount = session.RoomCount;
            List<string> RoomValues = new(roomCount);
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

                    var roomMedian = context.GetOrCompute(
                        $"med_room_{roomIndex}",
                        () => MetricHelper.ComputePercentile(roomValues, 50)
                    );

                    RoomValues.Add(roomMedian.ToString());
                }
            }

            return new MetricResult(segmentValue, RoomValues);
        }

        public static MetricResult MedianAbsoluteDeviation(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                             .OrderBy(t => t)
                             .ToList()
            );

            TimeTicks segmentMAD = MetricHelper.ComputeMAD(segmentValues);
            string segmentResult = segmentMAD.ToString();
            context.Set("mad_segment", segmentMAD);

            int roomCount = session.RoomCount;
            List<string> RoomValues = new(roomCount);
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
                    TimeTicks roomMAD = MetricHelper.ComputeMAD(roomValues);
                    context.Set($"mad_room_{roomIndex}", roomMAD);
                    RoomValues.Add(roomMAD.ToString());
                }
            }

            return new MetricResult(segmentResult, RoomValues);
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

        public static MetricResult SumOfBest(PracticeSession session, MetricContext context, bool isExport)
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
                    var minRoom = roomTimes.Count != 0 ? roomTimes.Min() : TimeTicks.Zero;
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

            if (segmentTimes.Count == 0)
            {
                return new MetricResult("", []);
            }

            int successCount = segmentTimes.Select(s => s <= targetTime).Count();
            double successRate = (double)successCount / session.TotalCompleted;
            context.Set("successRate", successRate);

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
            int percentile = MetricHelper.ToInt(SpeebrunConsistencyTrackerModule.Settings.PercentileValue);

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

        public static MetricResult InterquartileRange(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            TimeTicks Q1 = MetricHelper.ComputePercentile(segmentValues, 25);
            TimeTicks Q3 = MetricHelper.ComputePercentile(segmentValues, 75);
            context.Set("q1_segment", Q1);
            context.Set("q3_segment", Q3);
            string segmentResult = "[" + Q1 + ", " + Q3 + "]";

            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);

            if (isExport)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    string roomKey = $"room_{i}_values_sorted";
                    var times = context.GetOrCompute(
                        roomKey,
                        () => session.GetRoomTimes(i).OrderBy(t => t).ToList()
                    );

                    TimeTicks roomQ1 = MetricHelper.ComputePercentile(times, 25);
                    TimeTicks roomQ3 = MetricHelper.ComputePercentile(times, 75);
                    context.Set($"q1_room_{i}", roomQ1);
                    context.Set($"q3_room_{i}", roomQ3);
                    roomValues.Add("[" + roomQ1 + ", " + roomQ3 + "]");
                }
            }

            return new MetricResult(segmentResult, roomValues);
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
            string segmentValue = "";
            if (runCount != 0)
            {
                double segmentRate = (double)dnfCount / runCount;
                context.Set("resetRate_segment", segmentRate);
                segmentValue = MetricHelper.FormatPercent(segmentRate);
            }
            int roomCount = session.RoomCount;
            List<string> roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomDnfCount = session.DnfPerRoom.GetValueOrDefault((RoomIndex)index);
                    int roomRunCount = session.TotalAttemptsPerRoom.GetValueOrDefault((RoomIndex)index);
                    string roomValue = "";
                    if (roomRunCount != 0)
                    {
                        double roomRate = (double)roomDnfCount / roomRunCount;
                        context.Set($"resetRate_room_{index}", roomRate);
                        roomValue = MetricHelper.FormatPercent(roomRate);
                    }
                    roomValues.Add(roomValue);
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

        public static MetricResult ConsistencyScore(PracticeSession session, MetricContext context, bool isExport)
        {
            if (!isExport)
                return new MetricResult("", []);

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                var roomTimes = session.GetRoomTimes(r).ToList();
                double roomAvg = context.GetOrCompute($"avg_room_{r}", () => roomTimes.Average(t => t.Ticks));
                double roomMedian = context.GetOrCompute($"med_room_{r}", () => MetricHelper.ComputePercentile(roomTimes, 50)).Ticks;
                if (roomMedian <= 0) {
                    roomValues.Add("");
                    continue;
                }
                if (!context.TryGet($"std_room_{r}", out double roomSD))
                {
                    roomValues.Add("");
                    continue;
                }
                if (!context.TryGet($"mad_room_{r}", out double roomMAD))
                {
                    roomValues.Add("");
                    continue;
                }
                if (!context.TryGet($"resetRate_room_{r}", out double roomResetRate))
                {
                    roomValues.Add("");
                    continue;
                }
                if (!context.TryGet($"q1_room_{r}", out double roomQ1) || !context.TryGet($"q3_room_{r}", out double roomQ3))
                {
                    roomValues.Add("");
                    continue;
                }
                double roomIQR = roomQ3 - roomQ1;

                double relMad = Math.Max(0, 1.0 - (roomMAD / roomMedian));
                double relIqr = Math.Max(0, 1.0 - (roomIQR / roomMedian));

                double stability = (relMad * 30) + (relIqr * 30);
                double reliability = 1.0 - Math.Clamp(roomResetRate, 0, 1.0);
                double masteryBonus = 0;
                if (roomSD > 0)
                {
                    double npSkew = (roomAvg - roomMedian) / roomSD;
                    // Normalizing: Skew of 0.5+ gives full points, -0.5 gives 0 points
                    masteryBonus = Math.Clamp(npSkew + 0.5, 0, 1.0);
                }
                double finalScore = stability + (reliability * 30) + (masteryBonus * 10);
                roomValues.Add(MetricHelper.FormatPercent(finalScore));
            }

            string segmentValue = "";
            var segmentTimes = session.GetSegmentTimes().ToList();
            double segmentAvg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
            double segmentMedian = context.GetOrCompute("med_segment", () => MetricHelper.ComputePercentile(segmentTimes, 50)).Ticks;
            double targetTime = MetricEngine.GetTargetTimeTicks().Ticks;
            if (segmentMedian <= 0 || targetTime <= 0) return new MetricResult(segmentValue, roomValues);
            if (!context.TryGet("std_segment", out double segmentSD)) return new MetricResult(segmentValue, roomValues);
            if (!context.TryGet("mad_segment", out double segmentMad)) return new MetricResult(segmentValue, roomValues);
            if (!context.TryGet("q1_segment", out double segmentQ1) || !context.TryGet("q3_segment", out double segmentQ3)) return new MetricResult(segmentValue, roomValues);
            double segmentIQR = segmentQ3 - segmentQ1;
            if (!context.TryGet("resetRate_segment", out double segmentResetRate)) return new MetricResult(segmentValue, roomValues);
            if (!context.TryGet("successRate", out double successRate)) return new MetricResult(segmentValue, roomValues);

            // 1. RAW STABILITY (40 pts Total)
            // 20 pts for MAD + 20 pts for IQR. 
            // This is the core of the score: are you doing the same thing every time?
            double segmentRelMad = Math.Max(0, 1.0 - (segmentMad / segmentMedian));
            double segmentRelIqr = Math.Max(0, 1.0 - (segmentIQR / segmentMedian));
            double stabilityScore = (segmentRelMad * 20) + (segmentRelIqr * 20);

            // 2. PROXIMITY (10 pts)
            // How close is your typical run to your stated goal?
            double diff = Math.Abs(segmentMedian - targetTime);
            double proximityScore = Math.Max(0, 1.0 - (diff / targetTime)) * 10;

            // 3. SUCCESS BONUS (20 pts)
            // Small bonus for actually being under the target.
            double successScore = successRate * 20;

            // 4. RESET PENALTY (20 pts)
            // Heavy penalty for DNFs. If you can't finish, you aren't consistent.
            double reliabilityScore = (1.0 - Math.Clamp(segmentResetRate, 0, 1.0)) * 20;

            // 5. MASTERY (10 pts)
            // Non-Parametric Skew: Rewards the "Mountain on the Left" (Positive Skew).
            double masteryScore = 0;
            if (segmentSD > 0)
            {
                double npSkew = (segmentAvg - segmentMedian) / segmentSD;
                masteryScore = Math.Clamp(npSkew + 0.5, 0, 1.0) * 10;
            }       

            double segmentFinalScore = stabilityScore + proximityScore + reliabilityScore + successScore + masteryScore;
            segmentValue = MetricHelper.FormatPercent(segmentFinalScore);

            return new MetricResult(segmentValue, roomValues);
        }
    }
}
