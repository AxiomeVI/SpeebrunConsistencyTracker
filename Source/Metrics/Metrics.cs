using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using System.Globalization;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static partial class Metrics
    {
        private static int RoomCount => SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;

        public static MetricResult Average(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (segmentTimes.Count == 0)
                segmentValue = "0";
            else
            {
                double avg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
                segmentValue = new TimeTicks((long)Math.Round(avg)).ToString();
            }

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    double roomAvg = context.GetOrCompute($"avg_room_{r}", () => sorted.Average(t => t.Ticks));
                    return new TimeTicks((long)Math.Round(roomAvg)).ToString();
                });

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Median(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            var median = context.GetOrCompute("med_segment", () => MetricHelper.ComputePercentile(segmentValues, 50));
            string segmentValue = median.ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => context.GetOrCompute($"med_room_{r}", () => MetricHelper.ComputePercentile(sorted, 50)).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult MedianAbsoluteDeviation(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            TimeTicks segmentMAD = context.GetOrCompute("mad_segment", () => MetricHelper.ComputeMAD(segmentValues));

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => context.GetOrCompute($"mad_room_{r}", () => MetricHelper.ComputeMAD(sorted)).ToString());

            return new MetricResult(segmentMAD.ToString(), roomValues);
        }

        public static MetricResult RelativeMAD(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            double segmentMAD    = context.GetOrCompute("mad_segment", () => MetricHelper.ComputeMAD(segmentValues));
            double segmentMedian = context.GetOrCompute("med_segment", () => MetricHelper.ComputePercentile(segmentValues, 50));
            double relmad = segmentMedian == 0.0 ? 0.0 : context.GetOrCompute("relmad_segment", () => segmentMAD / segmentMedian);

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    TimeTicks roomMAD    = context.GetOrCompute($"mad_room_{r}", () => MetricHelper.ComputeMAD(sorted));
                    TimeTicks roomMedian = context.GetOrCompute($"med_room_{r}", () => MetricHelper.ComputePercentile(sorted, 50));
                    double relmadRoom = roomMedian == 0.0 ? 0.0 : context.GetOrCompute($"relmad_room_{r}", () => roomMAD / roomMedian);
                    return MetricHelper.FormatPercent(relmadRoom);
                });

            return new MetricResult(MetricHelper.FormatPercent(relmad), roomValues);
        }

        public static MetricResult StdDev(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (segmentTimes.Count < 2)
                segmentValue = "0";
            else
            {
                double avg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
                double stdSegment = context.GetOrCompute("std_segment", () => MetricHelper.ComputeStdDev(segmentTimes, avg));
                segmentValue = new TimeTicks((long)Math.Round(stdSegment)).ToString();
            }

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    double avgRoom = context.GetOrCompute($"avg_room_{r}", () => sorted.Average(t => t.Ticks));
                    double stdRoom = context.GetOrCompute($"std_room_{r}", () => MetricHelper.ComputeStdDev(sorted, avgRoom));
                    return new TimeTicks((long)Math.Round(stdRoom)).ToString();
                }, minCount: 2);

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult CoefVariation(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue;

            if (segmentTimes.Count < 2)
                segmentValue = "0";
            else
            {
                double avg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
                double std = context.GetOrCompute("std_segment", () => MetricHelper.ComputeStdDev(segmentTimes, avg));
                double cv = avg == 0.0 ? 0.0 : context.GetOrCompute("cv_segment", () => std / avg);
                segmentValue = MetricHelper.FormatPercent(cv);
            }

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    double avgRoom = context.GetOrCompute($"avg_room_{r}", () => sorted.Average(t => t.Ticks));
                    double stdRoom = context.GetOrCompute($"std_room_{r}", () => MetricHelper.ComputeStdDev(sorted, avgRoom));
                    double cv = avgRoom == 0.0 ? 0.0 : context.GetOrCompute($"cv_room_{r}", () => stdRoom / avgRoom);
                    return MetricHelper.FormatPercent(cv);
                }, minCount: 2);

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Best(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            string segmentValue = segmentSorted.Count == 0 ? "0"
                : context.GetOrCompute("min_segment", () => segmentSorted[0]).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => context.GetOrCompute($"min_room_{r}", () => sorted[0]).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Worst(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            string segmentValue = segmentSorted.Count == 0 ? "0"
                : context.GetOrCompute("max_segment", () => segmentSorted[^1]).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => context.GetOrCompute($"max_room_{r}", () => sorted[^1]).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult SumOfBest(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            var roomValues = new List<string>(roomCount);
            TimeTicks sumTicks = TimeTicks.Zero;

            for (int r = 0; r < roomCount; r++)
            {
                var sorted = context.GetOrCompute(
                    $"room_{r}_values_sorted",
                    () => session.GetRoomTimes(r).OrderBy(t => t).ToList()
                );
                if (sorted.Count == 0) { roomValues.Add(""); continue; }
                TimeTicks bestRoom = context.GetOrCompute($"min_room_{r}", () => sorted[0]);
                sumTicks += bestRoom;
                roomValues.Add(sumTicks.ToString());
            }

            return new MetricResult(sumTicks.ToString(), roomValues);
        }

        public static MetricResult SuccessRate(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            List<TimeTicks> segmentTimes = [.. session.GetSegmentTimes()];
            if (segmentTimes.Count == 0)
                return new MetricResult("", []);

            TimeTicks targetTime = MetricEngine.GetTargetTimeTicks();
            double successRate = segmentTimes.Count(s => s <= targetTime) / (double)session.TotalCompleted();

            var roomValues = new List<string>();
            if (isExport)
                for (int r = 0; r < roomCount; r++)
                    roomValues.Add("");

            return new MetricResult(MetricHelper.FormatPercent(successRate), roomValues);
        }

        public static MetricResult Percentile(PracticeSession session, MetricContext context, bool isExport)
        {
            int percentile = MetricHelper.ToInt(SpeebrunConsistencyTrackerModule.Settings.PercentileValue);

            var segmentSorted = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            string segmentValue = MetricHelper.ComputePercentile(segmentSorted, percentile).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => MetricHelper.ComputePercentile(sorted, percentile).ToString(), minCount: 0);

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult InterquartileRange(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );

            TimeTicks Q1 = context.GetOrCompute("q1_segment", () => MetricHelper.ComputePercentile(segmentValues, 25));
            TimeTicks Q3 = context.GetOrCompute("q3_segment", () => MetricHelper.ComputePercentile(segmentValues, 75));

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    TimeTicks roomQ1 = context.GetOrCompute($"q1_room_{r}", () => MetricHelper.ComputePercentile(sorted, 25));
                    TimeTicks roomQ3 = context.GetOrCompute($"q3_room_{r}", () => MetricHelper.ComputePercentile(sorted, 75));
                    return "[" + roomQ1 + "; " + roomQ3 + "]";
                }, minCount: 0);

            return new MetricResult("[" + Q1 + "; " + Q3 + "]", roomValues);
        }

        public static MetricResult CompletedRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            string segmentValue = session.TotalCompleted().ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.CompletedRunsPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult TotalRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            string segmentValue = session.TotalAttempts.ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.TotalAttemptsPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult DnfCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            string segmentValue = session.TotalDnfs().ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.DnfPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ResetRate(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            int dnfCount = session.TotalDnfs();
            int runCount = session.TotalAttempts;
            string segmentValue = "";
            if (runCount != 0)
            {
                double segmentRate = context.GetOrCompute("resetRate_segment", () => (double)dnfCount / runCount);
                segmentValue = MetricHelper.FormatPercent(segmentRate);
            }
            List<string> roomValues = new(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomDnfCount = session.DnfPerRoom.GetValueOrDefault(index);
                    int roomRunCount = session.TotalAttemptsPerRoom.GetValueOrDefault(index);
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
            int roomCount = RoomCount;
            if (!isExport)
                return new MetricResult("", []);

            int dnfCount = session.TotalDnfs();
            string segmentValue = dnfCount == 0 ? "0%" : "100%";
            List<string> roomValues = new(roomCount);
            for (int index = 0; index < roomCount; index++)
            {
                int roomDnfCount = session.DnfPerRoom.GetValueOrDefault(index);
                roomValues.Add(dnfCount == 0 ? "0%" : MetricHelper.FormatPercent((double)roomDnfCount / dnfCount));
            }
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult TrendSlope(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue = MetricHelper.LinearRegression(segmentTimes).ToString();

            var roomValues = new List<string>(roomCount);
            if (isExport)
                for (int r = 0; r < roomCount; r++)
                    roomValues.Add(MetricHelper.LinearRegression(session.GetRoomTimes(r).ToList()).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ConsistencyScore(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            if (session.TotalCompleted() < 2)
                return new MetricResult("100%", []);

            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = context.GetOrCompute(
                        $"room_{r}_values_sorted",
                        () => session.GetRoomTimes(r).OrderBy(t => t).ToList()
                    );
                    if (roomTimes.Count < 2) { roomValues.Add(""); continue; }
                    double roomAvg = context.GetOrCompute($"avg_room_{r}", () => roomTimes.Average(t => t.Ticks));
                    double stdRoom = context.GetOrCompute($"std_room_{r}", () => MetricHelper.ComputeStdDev(roomTimes, roomAvg));
                    double roomResetRate = context.GetOrCompute($"resetRate_room_{r}", () => (double)session.DnfPerRoom.GetValueOrDefault(r) / session.TotalAttemptsPerRoom.GetValueOrDefault(r));
                    double roomMedian = context.GetOrCompute($"med_room_{r}", () => MetricHelper.ComputePercentile(roomTimes, 50)).Ticks;
                    TimeTicks roomMin = context.GetOrCompute($"min_room_{r}", () => roomTimes[0]);
                    TimeTicks roomMAD = context.GetOrCompute($"mad_room_{r}", () => MetricHelper.ComputeMAD(roomTimes));
                    double roomCV = context.GetOrCompute($"cv_room_{r}", () => stdRoom / roomAvg);
                    double roomRelMAD = context.GetOrCompute($"relmad_room_{r}", () => roomMAD / roomMedian);
                    roomValues.Add(MetricHelper.FormatPercent(MetricHelper.ComputeConsistencyScore(roomMedian, roomMin, roomRelMAD, roomResetRate, roomCV)));
                }
            }

            var segmentTimes = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );
            double segmentAvg = context.GetOrCompute("avg_segment", () => segmentTimes.Average(t => t.Ticks));
            double stdSegment = context.GetOrCompute("std_segment", () => MetricHelper.ComputeStdDev(segmentTimes, segmentAvg));
            double segmentMedian = context.GetOrCompute("med_segment", () => MetricHelper.ComputePercentile(segmentTimes, 50)).Ticks;
            double segmentResetRate = context.GetOrCompute("resetRate_segment", () => (double)session.TotalDnfs() / session.TotalAttempts);
            TimeTicks segmentMin = context.GetOrCompute("min_segment", () => segmentTimes[0]);
            TimeTicks segmentMad = context.GetOrCompute("mad_segment", () => MetricHelper.ComputeMAD(segmentTimes));
            context.GetOrCompute("q1_segment", () => MetricHelper.ComputePercentile(segmentTimes, 25));
            context.GetOrCompute("q3_segment", () => MetricHelper.ComputePercentile(segmentTimes, 75));
            double cvSegment = context.GetOrCompute("cv_segment", () => stdSegment / segmentAvg);
            double relMADSegment = context.GetOrCompute("relmad_segment", () => segmentMad / segmentMedian);

            double score = MetricHelper.ComputeConsistencyScore(segmentMedian, segmentMin, relMADSegment, segmentResetRate, cvSegment);
            return new MetricResult(MetricHelper.FormatPercent(score), roomValues);
        }

        public static MetricResult MultimodalTest(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            if (!isExport)
                return new MetricResult("", []);
            if (session.TotalCompleted() < 10)
                return new MetricResult("Insufficent data", []);

            var segmentValues = context.GetOrCompute(
                "segment_values_sorted",
                () => session.GetSegmentTimes().OrderBy(t => t).ToList()
            );
            double avgSegment = context.GetOrCompute("avg_segment", () => segmentValues.Average(t => t.Ticks));
            double stdSegment = context.GetOrCompute("std_segment", () => MetricHelper.ComputeStdDev(segmentValues, avgSegment));
            TimeTicks segmentMin = context.GetOrCompute("min_segment", () => segmentValues[0]);
            TimeTicks segmentMax = context.GetOrCompute("max_segment", () => segmentValues[^1]);
            TimeTicks segmentQ1 = context.GetOrCompute("q1_segment", () => MetricHelper.ComputePercentile(segmentValues, 25));
            TimeTicks segmentQ3 = context.GetOrCompute("q3_segment", () => MetricHelper.ComputePercentile(segmentValues, 75));

            double bc = MetricHelper.CalculateBC(segmentValues, avgSegment);
            bool hasPhysicalGap = MetricHelper.DetectSignificantGap(segmentValues, stdSegment);
            bool isBimodal = bc > 0.555 && hasPhysicalGap;
            MetricHelper.PeakReport peak = MetricHelper.GetFullPeakAnalysis(segmentValues, segmentMin, segmentMax, segmentQ3 - segmentQ1, isBimodal);
            string segmentValue = bc.ToString("F3") + "; " + peak.Summary;

            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                var roomTimes = context.GetOrCompute(
                    $"room_{r}_values_sorted",
                    () => session.GetRoomTimes(r).OrderBy(t => t).ToList()
                );
                if (roomTimes.Count < 2) { roomValues.Add(""); continue; }
                double roomAvg = context.GetOrCompute($"avg_room_{r}", () => roomTimes.Average(t => t.Ticks));
                double stdRoom = context.GetOrCompute($"std_room_{r}", () => Math.Sqrt(roomTimes.Sum(t => Math.Pow(t.Ticks - roomAvg, 2)) / (roomTimes.Count - 1)));
                TimeTicks maxRoom = context.GetOrCompute($"max_room_{r}", () => roomTimes[^1]);
                TimeTicks minRoom = context.GetOrCompute($"min_room_{r}", () => roomTimes[0]);
                TimeTicks roomQ1 = context.GetOrCompute($"q1_room_{r}", () => MetricHelper.ComputePercentile(roomTimes, 25));
                TimeTicks roomQ3 = context.GetOrCompute($"q3_room_{r}", () => MetricHelper.ComputePercentile(roomTimes, 75));
                double bcRoom = MetricHelper.CalculateBC(roomTimes, roomAvg);
                bool hasPhysicalGapRoom = MetricHelper.DetectSignificantGap(roomTimes, stdRoom);
                bool isBimodalRoom = bcRoom > 0.555 && hasPhysicalGapRoom;
                MetricHelper.PeakReport peakRoom = MetricHelper.GetFullPeakAnalysis(roomTimes, minRoom, maxRoom, roomQ3 - roomQ1, isBimodalRoom);
                roomValues.Add(bcRoom.ToString("F3") + "; " + peakRoom.Summary);
            }

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult GoldRate(PracticeSession session, MetricContext context, bool isExport)
        {
            var seg = session.GetSegmentTimes().ToList();
            string segmentValue;
            if (seg.Count == 0)
                segmentValue = "";
            else
            {
                long bestSeg = seg.Min(t => t.Ticks);
                double segRate = (double)seg.Count(t => t.Ticks == bestSeg) / seg.Count;
                segmentValue = MetricHelper.FormatPercent(segRate);
            }

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => {
                    long gold = sorted[0].Ticks;
                    double rate = (double)sorted.Count(t => t.Ticks == gold) / sorted.Count;
                    return MetricHelper.FormatPercent(rate);
                });

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult RoomDependency(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = RoomCount;
            if (!isExport)
                return new MetricResult("", []);
            if (session.TotalAttempts < 10)
                return new MetricResult("Insufficent data", []);

            List<Attempt> attempts = [.. session.Attempts];
            var roomValues = new List<string> { "" };

            for (int i = 0; i < roomCount - 1; i++)
            {
                var x = new List<double>();
                var y = new List<double>();
                foreach (var attempt in attempts)
                {
                    if (i + 1 < attempt.CompletedRooms.Count)
                    {
                        x.Add((double)attempt.CompletedRooms[i]);
                        y.Add((double)attempt.CompletedRooms[i + 1]);
                    }
                }
                roomValues.Add((x.Count < 5) ? "" : MetricHelper.CalculatePearson(x, y).ToString("F2", CultureInfo.InvariantCulture));
            }

            return new MetricResult("", roomValues);
        }
    }
}
