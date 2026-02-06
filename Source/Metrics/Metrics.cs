using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using System.Globalization;

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

            TimeTicks segmentMAD = context.GetOrCompute("mad_segment", () => MetricHelper.ComputeMAD(segmentValues));
            string segmentResult = segmentMAD.ToString();

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
                    TimeTicks roomMAD = context.GetOrCompute($"mad_room_{roomIndex}", () => MetricHelper.ComputeMAD(roomValues));
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
            }
            else
            {
                // Reuse average if available, otherwise compute
                double avg = context.GetOrCompute("avg_segment", () =>
                    segmentTimes.Average(t => t.Ticks));
                double stdSegment = context.GetOrCompute("std_segment", () => Math.Sqrt(segmentTimes.Sum(t => Math.Pow(t.Ticks - avg, 2)) / (segmentTimes.Count - 1)));
                segmentValue = new TimeTicks((long)Math.Round(stdSegment)).ToString();
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

                        double variance = roomTimes.Sum(t => Math.Pow(t.Ticks - avgRoom, 2)) / (roomTimes.Count - 1);
                        double stdRoom = context.GetOrCompute($"std_room_{r}", () => Math.Sqrt(roomTimes.Sum(t => Math.Pow(t.Ticks - avgRoom, 2)) / (roomTimes.Count - 1)));
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

                double cv = avg == 0.0 ? 0.0 : context.GetOrCompute("cv_segment", () => std / avg);
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
                            double variance = roomTimes.Sum(v => Math.Pow(v.Ticks - avgRoom, 2)) / (roomTimes.Count - 1);
                            return Math.Sqrt(variance);
                        });
                        double cv = avgRoom == 0.0 ? 0.0 : context.GetOrCompute($"cv_room_{r}", () => stdRoom / avgRoom);
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
            }
            else
            {
                TimeTicks best = context.GetOrCompute("min_segment", () => segmentSorted[0]);
                segmentValue = best.ToString();
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
                    }
                    else
                    {
                        TimeTicks best = context.GetOrCompute($"min_room_{r}", () => roomSorted[0]);
                        roomValues.Add(best.ToString());
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

            string segmentValue;
            if (segmentSorted.Count == 0)
                segmentValue = "0";
            else
            {
                TimeTicks worst = context.GetOrCompute("max_segment", () => segmentSorted[^1]);
                segmentValue = worst.ToString();
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

                    if (roomSorted.Count != 0)
                        roomValues.Add("0");
                    else
                    {
                        TimeTicks worstRoom = context.GetOrCompute($"max_room_{r}", () => roomSorted[^1]);
                        roomValues.Add(worstRoom.ToString());
                    }
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

        public static MetricResult SuccessRate(PracticeSession session, MetricContext context, bool isExport)
        {
            List<TimeTicks> segmentTimes = [.. session.GetSegmentTimes()];
            TimeTicks targetTime = MetricEngine.GetTargetTimeTicks();
            if (segmentTimes.Count == 0)
            {
                return new MetricResult("", []);
            }

            double successCount = segmentTimes.Count(s => s <= targetTime);
            double successRate = successCount / session.TotalCompleted;

            var roomValues = new List<string>();
            if (isExport)
            {
                int roomCount = session.RoomCount;
                for (int r = 0; r < roomCount; r++)
                {
                    roomValues.Add("");
                }
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
            List<string> RoomValues = new(roomCount);
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

            TimeTicks Q1 = context.GetOrCompute("q1_segment", () => MetricHelper.ComputePercentile(segmentValues, 25));
            TimeTicks Q3 = context.GetOrCompute("q3_segment", () => MetricHelper.ComputePercentile(segmentValues, 75));
            string segmentResult = "[" + Q1 + "; " + Q3 + "]";

            int roomCount = session.RoomCount;
            List<string> roomValues = new(roomCount);

            if (isExport)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    string roomKey = $"room_{i}_values_sorted";
                    var times = context.GetOrCompute(
                        roomKey,
                        () => session.GetRoomTimes(i).OrderBy(t => t).ToList()
                    );

                    TimeTicks roomQ1 = context.GetOrCompute($"q1_room_{i}", () => MetricHelper.ComputePercentile(times, 25));
                    TimeTicks roomQ3 = context.GetOrCompute($"q3_room_{i}", () => MetricHelper.ComputePercentile(times, 75));
                    roomValues.Add("[" + roomQ1 + "; " + roomQ3 + "]");
                }
            }

            return new MetricResult(segmentResult, roomValues);
        }

        public static MetricResult CompletedRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue = session.TotalCompleted.ToString();
            int roomCount = session.RoomCount;
            List<string> roomValues = new(roomCount);
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
            List<string> roomValues = new(roomCount);
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
            List<string> roomValues = new(roomCount);
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
                double segmentRate = context.GetOrCompute("resetRate_segment", () => (double)dnfCount / runCount);
                segmentValue = MetricHelper.FormatPercent(segmentRate);
            }
            int roomCount = session.RoomCount;
            List<string> roomValues = new(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomDnfCount = session.DnfPerRoom.GetValueOrDefault((RoomIndex)index);
                    int roomRunCount = session.TotalAttemptsPerRoom.GetValueOrDefault((RoomIndex)index);
                    string roomValue = "";
                    if (roomRunCount != 0)
                    {
                        double roomRate = context.GetOrCompute($"resetRate_room_{index}", () => (double)roomDnfCount / roomRunCount);
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
            List<string> roomValues = new(roomCount);
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
            int attempts = session.TotalCompleted;
            if (!isExport || attempts < 2)
                return new MetricResult("", []);

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                string roomKey = $"room_{r}_values_sorted";
                var roomTimes = context.GetOrCompute(
                    roomKey,
                    () => session.GetRoomTimes(r)
                                .OrderBy(t => t)
                                .ToList()
                );
                double roomAvg = context.GetOrCompute($"avg_room_{r}", () => roomTimes.Average(t => t.Ticks));
                double stdRoom = context.GetOrCompute($"std_room_{r}", () => Math.Sqrt(roomTimes.Sum(t => Math.Pow(t.Ticks - roomAvg, 2)) / (roomTimes.Count - 1)));
                double roomResetRate = context.GetOrCompute($"resetRate_room_{r}", () => (double)session.DnfPerRoom.GetValueOrDefault((RoomIndex)r) / session.TotalAttemptsPerRoom.GetValueOrDefault((RoomIndex)r));
                double roomMedian = context.GetOrCompute($"med_room_{r}", () => MetricHelper.ComputePercentile(roomTimes, 50)).Ticks;
                TimeTicks roomMin = context.GetOrCompute($"min_room_{r}", () => roomTimes[0]);
                TimeTicks roomMAD = context.GetOrCompute($"mad_room_{r}", () => MetricHelper.ComputeMAD(roomTimes));
                double roomCV = context.GetOrCompute($"cv_room_{r}", () => stdRoom / roomAvg);

                double finalScore = MetricHelper.ComputeConsistencyScore(roomMedian, roomMin, roomMAD, roomResetRate, roomCV);
                roomValues.Add(MetricHelper.FormatPercent(finalScore));
            }

            var segmentTimes = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                             .OrderBy(t => t)
                             .ToList()
            );
            double segmentAvg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
            double stdSegment = context.GetOrCompute("std_segment", () => Math.Sqrt(segmentTimes.Sum(t => Math.Pow(t.Ticks - segmentAvg, 2)) / (segmentTimes.Count - 1)));
            double segmentMedian = context.GetOrCompute("med_segment", () => MetricHelper.ComputePercentile(segmentTimes, 50)).Ticks;
            double segmentResetRate = context.GetOrCompute("resetRate_segment", () => (double)session.TotalDnfs / session.TotalAttempts);
            TimeTicks segmentMin = context.GetOrCompute("min_segment", () => segmentTimes[0]);
            TimeTicks segmentMad = context.GetOrCompute("mad_segment", () => MetricHelper.ComputeMAD(segmentTimes));
            TimeTicks segmentQ1 = context.GetOrCompute("q1_segment", () => MetricHelper.ComputePercentile(segmentTimes, 25));
            TimeTicks segmentQ3 = context.GetOrCompute("q3_segment", () => MetricHelper.ComputePercentile(segmentTimes, 75));
            double cvSegment = context.GetOrCompute("cv_segment", () => stdSegment / segmentAvg);

            double segmentFinalScore = MetricHelper.ComputeConsistencyScore(segmentMedian, segmentMin, segmentMad, segmentResetRate, cvSegment);
            string segmentValue = MetricHelper.FormatPercent(segmentFinalScore);
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult MultimodalTest(PracticeSession session, MetricContext context, bool isExport)
        {
            if (!isExport)
                return new MetricResult("", []);
            int attempts = session.TotalCompleted;
            if (attempts < 10)
                return new MetricResult("Insufficent data", []);
            
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes()
                             .OrderBy(t => t)
                             .ToList()
            );
            double avgSegment = context.GetOrCompute("avg_segment", () =>
                    segmentValues.Average(t => t.Ticks));
            double stdSegment = context.GetOrCompute("std_segment", () => Math.Sqrt(segmentValues.Sum(t => Math.Pow(t.Ticks - avgSegment, 2)) / (segmentValues.Count - 1)));
            TimeTicks segmentMin = context.GetOrCompute("min_segment", () => segmentValues[0]);
            TimeTicks segmentMax = context.GetOrCompute("max_segment", () => segmentValues[^1]);
            
            double bc = MetricHelper.CalculateBC(segmentValues, avgSegment);
            bool hasPhysicalGap = MetricHelper.DetectSignificantGap(segmentValues, stdSegment);
            bool isBimodal = bc > 0.555 && hasPhysicalGap;
            MetricHelper.PeakReport peak = MetricHelper.GetFullPeakAnalysis(segmentValues, segmentMin, segmentMax, isBimodal);
            string segmentValue = bc.ToString("F3")  + "; " + peak.Summary;

            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                var roomTimes = context.GetOrCompute(
                    $"room_{r}_values_sorted",
                    () => session.GetRoomTimes(r)
                                .OrderBy(t => t)
                                .ToList()
                );
                double roomAvg = context.GetOrCompute($"avg_room_{r}", () => roomTimes.Average(t => t.Ticks));
                double stdRoom = context.GetOrCompute($"std_room_{r}", () => Math.Sqrt(roomTimes.Sum(t => Math.Pow(t.Ticks - roomAvg, 2)) / (roomTimes.Count - 1)));
                TimeTicks maxRoom = context.GetOrCompute($"max_room_{r}", () => roomTimes[^1]);
                TimeTicks minRoom = context.GetOrCompute($"min_room_{r}", () => roomTimes[0]);
                double bcRoom = MetricHelper.CalculateBC(roomTimes, roomAvg);
                bool hasPhysicalGapRoom = MetricHelper.DetectSignificantGap(roomTimes, stdRoom);
                bool isBimodalRoom = bcRoom > 0.555 && hasPhysicalGapRoom;
                MetricHelper.PeakReport peakRoom = MetricHelper.GetFullPeakAnalysis(roomTimes, minRoom, maxRoom, isBimodalRoom);
                roomValues.Add(bcRoom.ToString("F3") + "; " + peakRoom.Summary);
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult RoomDepency(PracticeSession session, MetricContext context, bool isExport)
        {
            if (!isExport)
                return new MetricResult("", []);
            int totalAttempts = session.TotalAttempts;
            if (totalAttempts < 10)
                return new MetricResult("Insufficent data", []);

            List<Attempt> attempts = [.. session.Attempts];

            var roomValues = new List<string>
            {
                "" // First room doesn't depend on the previous one
            };
            int roomCount = session.RoomCount;
            for (int i = 0; i < roomCount; i++)
            {
                var x = new List<double>(); // Room i times
                var y = new List<double>(); // Room i+1 times
                foreach (var attempt in attempts)
                {
                    // Only correlate if BOTH rooms exist in this specific attempt
                    if (attempt.CompletedRooms.TryGetValue(new RoomIndex(i), out var ticksA) &&
                        attempt.CompletedRooms.TryGetValue(new RoomIndex(i + 1), out var ticksB))
                    {
                        x.Add((double)ticksA);
                        y.Add((double)ticksB);
                    }
                }
                roomValues.Add((x.Count < 5) ? "" : MetricHelper.CalculatePearson(x, y).ToString("F2", CultureInfo.InvariantCulture));
            }

            return new MetricResult("", roomValues);
        }
    }
}
