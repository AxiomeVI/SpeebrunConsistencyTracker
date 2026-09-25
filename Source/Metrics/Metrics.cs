using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System.Globalization;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static partial class Metrics
    {
        public static MetricResult Average(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue = segmentTimes.Count == 0
                ? "0"
                : new TimeTicks((long)Math.Round(MetricHelper.SegmentAverage(session, context))).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => new TimeTicks((long)Math.Round(MetricHelper.RoomAverage(session, context, r))).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Median(PracticeSession session, MetricContext context, bool isExport)
        {
            string segmentValue = MetricHelper.SegmentMedian(session, context).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => MetricHelper.RoomMedian(session, context, r).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult MedianAbsoluteDeviation(PracticeSession session, MetricContext context, bool isExport)
        {
            TimeTicks segmentMAD = MetricHelper.SegmentMAD(session, context);

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => MetricHelper.RoomMAD(session, context, r).ToString());

            return new MetricResult(segmentMAD.ToString(), roomValues);
        }

        public static MetricResult RelativeMAD(PracticeSession session, MetricContext context, bool isExport)
        {
            double segmentMedian = MetricHelper.SegmentMedian(session, context);
            double relmad = segmentMedian == 0.0 ? 0.0 : MetricHelper.SegmentRelativeMAD(session, context);

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => {
                    double roomMedian = MetricHelper.RoomMedian(session, context, r);
                    double relmadRoom = roomMedian == 0.0 ? 0.0 : MetricHelper.RoomRelativeMAD(session, context, r);
                    return MetricHelper.FormatPercent(relmadRoom);
                });

            return new MetricResult(MetricHelper.FormatPercent(relmad), roomValues);
        }

        public static MetricResult StdDev(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentTimes = session.GetSegmentTimes().ToList();
            string segmentValue = segmentTimes.Count < 2
                ? "0"
                : new TimeTicks((long)Math.Round(MetricHelper.SegmentStdDev(session, context))).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => new TimeTicks((long)Math.Round(MetricHelper.RoomStdDev(session, context, r))).ToString(),
                minCount: 2);

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
                double avg = MetricHelper.SegmentAverage(session, context);
                double cv = avg == 0.0 ? 0.0 : MetricHelper.SegmentCV(session, context);
                segmentValue = MetricHelper.FormatPercent(cv);
            }

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => {
                    double avgRoom = MetricHelper.RoomAverage(session, context, r);
                    double cv = avgRoom == 0.0 ? 0.0 : MetricHelper.RoomCV(session, context, r);
                    return MetricHelper.FormatPercent(cv);
                }, minCount: 2);

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Best(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = MetricHelper.SortedSegmentValues(session, context);

            string segmentValue = segmentSorted.Count == 0 ? "0"
                : MetricHelper.SegmentMin(session, context).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => MetricHelper.RoomMin(session, context, r).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult Worst(PracticeSession session, MetricContext context, bool isExport)
        {
            var segmentSorted = MetricHelper.SortedSegmentValues(session, context);

            string segmentValue = segmentSorted.Count == 0 ? "0"
                : MetricHelper.SegmentMax(session, context).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => MetricHelper.RoomMax(session, context, r).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult SumOfBest(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            var roomValues = new List<string>(roomCount);
            TimeTicks sumTicks = TimeTicks.Zero;

            for (int r = 0; r < roomCount; r++)
            {
                var sorted = MetricHelper.SortedRoomValues(session, context, r);
                if (sorted.Count == 0) { roomValues.Add(""); continue; }
                TimeTicks bestRoom = MetricHelper.RoomMin(session, context, r);
                sumTicks += bestRoom;
                roomValues.Add(sumTicks.ToString());
            }

            return new MetricResult(sumTicks.ToString(), roomValues);
        }

        public static MetricResult BestSplit(PracticeSession session, MetricContext context, bool isExport)
        {
            if (!isExport)
                return new MetricResult("", []);

            int roomCount = session.RoomCount;
            long[] bestCumul = new long[roomCount];
            Array.Fill(bestCumul, long.MaxValue);

            for (int a = 0; a < session.AttemptCount; a++)
            {
                int contig = session.ContiguousCount(a);
                int limit  = Math.Min(contig, roomCount);
                long cumul = 0;
                for (int r = 0; r < limit; r++)
                {
                    cumul += session.GetCell(a, r).Time.Ticks;
                    if (cumul < bestCumul[r])
                        bestCumul[r] = cumul;
                }
            }

            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
                roomValues.Add(bestCumul[r] == long.MaxValue ? "" : new TimeTicks(bestCumul[r]).ToString());

            // Shared keys — GetOrCompute is idempotent, so evaluation order with Best doesn't matter.
            var segmentSorted = MetricHelper.SortedSegmentValues(session, context);
            string segmentValue = segmentSorted.Count == 0
                ? "0"
                : MetricHelper.SegmentMin(session, context).ToString();

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult SuccessRate(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            List<TimeTicks> segmentTimes = [.. session.GetSegmentTimes()];
            if (segmentTimes.Count == 0)
                return new MetricResult("", []);

            TimeTicks targetTime = context.TargetTime;
            // Denominator from the same list as the numerator. TotalCompleted agrees with it
            // today, but it is counted by a different pass over the matrix.
            double successRate = segmentTimes.Count(s => s <= targetTime) / (double)segmentTimes.Count;

            var roomValues = new List<string>();
            if (isExport)
                for (int r = 0; r < roomCount; r++)
                    roomValues.Add("");

            return new MetricResult(MetricHelper.FormatPercent(successRate), roomValues);
        }

        public static MetricResult Percentile(PracticeSession session, MetricContext context, bool isExport)
        {
            int percentile = context.Percentile;

            var segmentSorted = MetricHelper.SortedSegmentValues(session, context);

            string segmentValue = MetricHelper.ComputePercentile(segmentSorted, percentile).ToString();

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, sorted) => MetricHelper.ComputePercentile(sorted, percentile).ToString(), minCount: 0);

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult InterquartileRange(PracticeSession session, MetricContext context, bool isExport)
        {
            TimeTicks Q1 = MetricHelper.SegmentQ1(session, context);
            TimeTicks Q3 = MetricHelper.SegmentQ3(session, context);

            var roomValues = MetricHelper.ComputeRoomValues(isExport, session, context,
                (r, _) => {
                    TimeTicks roomQ1 = MetricHelper.RoomQ1(session, context, r);
                    TimeTicks roomQ3 = MetricHelper.RoomQ3(session, context, r);
                    return (roomQ3 - roomQ1).ToString();
                }, minCount: 0);

            return new MetricResult((Q3 - Q1).ToString(), roomValues);
        }

        public static MetricResult CompletedRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            string segmentValue = session.TotalCompleted.ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.CompletedRunsPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult TotalRunCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            string segmentValue = session.TotalAttempts.ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.TotalAttemptsPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult DnfCount(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            string segmentValue = session.TotalDnfs.ToString();
            List<string> roomValues = new(roomCount);
            if (isExport)
                for (int index = 0; index < roomCount; index++)
                    roomValues.Add(session.DnfPerRoom.GetValueOrDefault(index).ToString());

            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ResetRate(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            int runCount = session.TotalAttempts;
            string segmentValue = runCount != 0
                ? MetricHelper.FormatPercent(MetricHelper.SegmentResetRate(session, context))
                : "";

            List<string> roomValues = new(roomCount);
            if (isExport)
            {
                for (int index = 0; index < roomCount; index++)
                {
                    int roomRunCount = session.TotalAttemptsPerRoom.GetValueOrDefault(index);
                    roomValues.Add(roomRunCount != 0
                        ? MetricHelper.FormatPercent(MetricHelper.RoomResetRate(session, context, index))
                        : "");
                }
            }
            return new MetricResult(segmentValue, roomValues);
        }

        public static MetricResult ResetShare(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            if (!isExport)
                return new MetricResult("", []);

            int dnfCount = session.TotalDnfs;
            // The share of resets is per room; the segment column has nothing to hold, and the
            // 100% it used to print read as a statistic.
            string segmentValue = "";
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
            int roomCount = session.RoomCount;
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
            int roomCount = session.RoomCount;
            if (session.TotalCompleted < 2)
                return new MetricResult("100%", []);

            var roomValues = new List<string>(roomCount);
            if (isExport)
            {
                for (int r = 0; r < roomCount; r++)
                {
                    var roomTimes = MetricHelper.SortedRoomValues(session, context, r);
                    if (roomTimes.Count < 2) { roomValues.Add(""); continue; }
                    double roomResetRate = MetricHelper.RoomResetRate(session, context, r);
                    double roomMedian = MetricHelper.RoomMedian(session, context, r).Ticks;
                    TimeTicks roomMin = MetricHelper.RoomMin(session, context, r);
                    double roomCV = MetricHelper.RoomCV(session, context, r);
                    double roomRelMAD = MetricHelper.RoomRelativeMAD(session, context, r);
                    roomValues.Add(MetricHelper.FormatPercent(MetricHelper.ComputeConsistencyScore(roomMedian, roomMin, roomRelMAD, roomResetRate, roomCV)));
                }
            }

            double segmentMedian = MetricHelper.SegmentMedian(session, context).Ticks;
            double segmentResetRate = MetricHelper.SegmentResetRate(session, context);
            TimeTicks segmentMin = MetricHelper.SegmentMin(session, context);
            double cvSegment = MetricHelper.SegmentCV(session, context);
            double relMADSegment = MetricHelper.SegmentRelativeMAD(session, context);

            double score = MetricHelper.ComputeConsistencyScore(segmentMedian, segmentMin, relMADSegment, segmentResetRate, cvSegment);
            return new MetricResult(MetricHelper.FormatPercent(score), roomValues);
        }

        public static MetricResult MultimodalTest(PracticeSession session, MetricContext context, bool isExport)
        {
            int roomCount = session.RoomCount;
            if (!isExport)
                return new MetricResult("", []);
            if (session.TotalCompleted < 10)
                return new MetricResult("Insufficient data", []);

            var segmentValues = MetricHelper.SortedSegmentValues(session, context);
            double avgSegment = MetricHelper.SegmentAverage(session, context);
            double stdSegment = MetricHelper.SegmentStdDev(session, context);
            TimeTicks segmentMin = MetricHelper.SegmentMin(session, context);
            TimeTicks segmentMax = MetricHelper.SegmentMax(session, context);
            TimeTicks segmentQ1 = MetricHelper.SegmentQ1(session, context);
            TimeTicks segmentQ3 = MetricHelper.SegmentQ3(session, context);

            double bc = MetricHelper.CalculateBC(segmentValues, avgSegment);
            bool hasPhysicalGap = MetricHelper.DetectSignificantGap(segmentValues, stdSegment);
            bool isBimodal = bc > 0.555 && hasPhysicalGap;
            MetricHelper.PeakReport peak = MetricHelper.GetFullPeakAnalysis(segmentValues, segmentMin, segmentMax, segmentQ3 - segmentQ1, isBimodal);
            string segmentValue = bc.ToString("F3", CultureInfo.InvariantCulture) + "; " + peak.Summary;

            var roomValues = new List<string>(roomCount);
            for (int r = 0; r < roomCount; r++)
            {
                var roomTimes = MetricHelper.SortedRoomValues(session, context, r);
                // BC needs n >= 4: its sample correction divides by (n-2)(n-3).
                if (roomTimes.Count < 4) { roomValues.Add(""); continue; }
                double roomAvg = MetricHelper.RoomAverage(session, context, r);
                double stdRoom = MetricHelper.RoomStdDev(session, context, r);
                TimeTicks maxRoom = MetricHelper.RoomMax(session, context, r);
                TimeTicks minRoom = MetricHelper.RoomMin(session, context, r);
                TimeTicks roomQ1 = MetricHelper.RoomQ1(session, context, r);
                TimeTicks roomQ3 = MetricHelper.RoomQ3(session, context, r);
                double bcRoom = MetricHelper.CalculateBC(roomTimes, roomAvg);
                bool hasPhysicalGapRoom = MetricHelper.DetectSignificantGap(roomTimes, stdRoom);
                bool isBimodalRoom = bcRoom > 0.555 && hasPhysicalGapRoom;
                MetricHelper.PeakReport peakRoom = MetricHelper.GetFullPeakAnalysis(roomTimes, minRoom, maxRoom, roomQ3 - roomQ1, isBimodalRoom);
                roomValues.Add(bcRoom.ToString("F3", CultureInfo.InvariantCulture) + "; " + peakRoom.Summary);
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
            int roomCount = session.RoomCount;
            if (!isExport)
                return new MetricResult("", []);
            if (session.TotalAttempts < 10)
                return new MetricResult("Insufficient data", []);

            var roomValues = new List<string> { "" };

            for (int i = 0; i < roomCount - 1; i++)
            {
                var x = new List<double>();
                var y = new List<double>();
                for (int a = 0; a < session.AttemptCount; a++)
                {
                    if (session.ContiguousCount(a) > i + 1)
                    {
                        x.Add((double)session.GetCell(a, i).Time);
                        y.Add((double)session.GetCell(a, i + 1).Time);
                    }
                }
                roomValues.Add((x.Count < 5) ? "" : MetricHelper.CalculatePearson(x, y).ToString("F2", CultureInfo.InvariantCulture));
            }

            return new MetricResult("", roomValues);
        }
    }
}
