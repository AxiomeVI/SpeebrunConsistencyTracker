using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Globalization;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public sealed class MetricContext
    {
        private readonly Dictionary<string, object> _cache = new();

        public T GetOrCompute<T>(string key, Func<T> compute)
        {
            if (_cache.TryGetValue(key, out var obj))
            {
                return (T)obj;
            }

            var value = compute();
            _cache[key] = value;
            return value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var obj))
            {
                value = (T)obj;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            _cache[key] = value!;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }

    public sealed class MetricDescriptor : IEquatable<MetricDescriptor>
    {
        public Func<string> CsvHeader { get; }

        public Func<string> InGameName { get; }

        public Func<PracticeSession, MetricContext, bool, MetricResult> Compute { get; }

        public Func<MetricOutput, bool> IsEnabled { get; }


        public MetricDescriptor(
            Func<string> csvHeader,
            Func<string> inGameName,
            Func<PracticeSession, MetricContext, bool, MetricResult> compute,
            Func<MetricOutput, bool> isEnabled)
        {
            CsvHeader = csvHeader ?? throw new ArgumentNullException(nameof(csvHeader));
            InGameName = inGameName ?? throw new ArgumentNullException(nameof(inGameName));
            Compute = compute ?? throw new ArgumentNullException(nameof(compute));
            IsEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
        }

        public MetricDescriptor(
            string csvHeader,
            string inGameName,
            Func<PracticeSession, MetricContext, bool, MetricResult> compute,
            Func<MetricOutput, bool> isEnabled)
            : this(() => csvHeader, () => inGameName, compute, isEnabled)
        { }

        public bool Equals(MetricDescriptor other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return InGameName() == other.InGameName();
        }

        public override bool Equals(object obj)
            => Equals(obj as MetricDescriptor);
        
        public override int GetHashCode()
            => InGameName().GetHashCode();
    }

    public sealed class MetricResult
    {
        public string SegmentValue { get; init; }
        public IReadOnlyList<string> RoomValues { get; init; }

        public MetricResult(string segmentValue, IReadOnlyList<string> roomValues)
        {
            SegmentValue = segmentValue ?? throw new ArgumentNullException(nameof(segmentValue));
            RoomValues = roomValues ?? throw new ArgumentNullException(nameof(roomValues));
        }
    }

    public static class MetricHelper
    {
        public static bool IsMetricEnabled(object value, MetricOutput mode)
        {
            return value switch
            {
                bool b => b && mode == MetricOutput.Export,
                MetricOutputChoice choice => (FromChoice(choice) & mode) != 0,
                _ => throw new InvalidOperationException($"Unsupported type {value.GetType()}"),
            };
        }

        private static MetricOutput FromChoice(MetricOutputChoice choice) => choice switch
        {
            MetricOutputChoice.Off => MetricOutput.Off,
            MetricOutputChoice.Overlay => MetricOutput.Overlay,
            MetricOutputChoice.Export => MetricOutput.Export,
            MetricOutputChoice.Both => MetricOutput.Overlay | MetricOutput.Export,
            _ => MetricOutput.Off
        };

        public static string FormatPercent(double value) =>
            (value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

        public static int ToInt(PercentileChoice choice) {
            return choice switch {
                PercentileChoice.P10 => 10,
                PercentileChoice.P20 => 20,
                PercentileChoice.P30 => 30,
                PercentileChoice.P40 => 40,
                PercentileChoice.P60 => 60,
                PercentileChoice.P70 => 70,
                PercentileChoice.P80 => 80,
                PercentileChoice.P90 => 90,
                _ => 0
            };
        }

        public static TimeTicks ComputePercentile(IList<TimeTicks> sortedValues, int _percentile)
        {
            double percentile = _percentile;
            int count = sortedValues.Count;
            if (count == 0)
                return TimeTicks.Zero;

            if (count == 1)
                return sortedValues[0];

            // Clamp to [0,100]
            percentile = Math.Max(0, Math.Min(100, percentile));

            double position = percentile / 100.0 * (count - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            double fraction = position - lowerIndex;
            double interpolated =
                sortedValues[lowerIndex].Ticks +
                fraction * (sortedValues[upperIndex].Ticks - sortedValues[lowerIndex].Ticks);

            return new TimeTicks((long)Math.Round(interpolated));
        }

        public static TimeTicks LinearRegression(IList<TimeTicks> values) {
            int n = values.Count;
            if (n <= 1) return TimeTicks.Zero;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

            for (int i = 0; i < n; i++) {
                double xi = i + 1;                 
                double yi = values[i].Ticks;       
                sumX += xi;
                sumY += yi;
                sumXY += xi * yi;
                sumX2 += xi * xi;
            }

            double denominator = n * sumX2 - sumX * sumX;
            if (denominator == 0) return TimeTicks.Zero;

            double slope = (n * sumXY - sumX * sumY) / denominator;

            return new TimeTicks((long)Math.Round(slope));
        }

        public static TimeTicks ComputeMAD(IList<TimeTicks> sortedTimes)
        {
            if (sortedTimes == null || sortedTimes.Count == 0) return TimeTicks.Zero;

            // Use your existing percentile logic to find the median
            double median = ComputePercentile(sortedTimes, 50).Ticks;

            // Calculate absolute deviations from the median
            var deviations = sortedTimes
                .Select(t => (long)Math.Round(Math.Abs(t.Ticks - median)))
                .OrderBy(d => d);

            // The MAD is the median of those deviations
            return ComputePercentile([.. deviations.Select(t => new TimeTicks(t))], 50);
        }

        public static double ComputeConsistencyScore(double median, TimeTicks min, double mad, double resetRate, double q1, double q3)
        {
            double IQR = q3 - q1;

            double relMad = median != 0 ? Math.Max(0, 1.0 - (mad / median)) : 0;
            double relIqr = median != 0 ? Math.Max(0, 1.0 - (IQR / median)) : 0;

            double stability = (relMad * 25) + (relIqr * 25);
            double completionRate = 1.0 - Math.Clamp(resetRate, 0, 1.0);
            double reliability = completionRate * completionRate;
            double floorProximity = 1;
            if (median > 0)
            {
                double gap = (median - (double)min) / median;
                floorProximity = Math.Max(0, 1.0 - (gap * 2.0));
            }
            return (stability + (floorProximity * 50)) * reliability / 100;
        }

        public static double CalculateBC(List<TimeTicks> values, double mean)
        {
            int n = values.Count;
            
            // Moments
            double m2 = 0, m3 = 0, m4 = 0;
            foreach (var x in values)
            {
                double d = (double)x - mean;
                double d2 = d * d;
                m2 += d2;
                m3 += d2 * d;
                m4 += d2 * d2;
            }
            m2 /= n; m3 /= n; m4 /= n;

            double skew = m3 / Math.Pow(m2, 1.5);
            double kurtosis = (m4 / (m2 * m2)) - 3; // Excess Kurtosis

            // Bimodality Coefficient Formula
            double numerator = (skew * skew) + 1;
            double sampleCorrection = 3.0 * Math.Pow(n - 1, 2) / ((n - 2) * (n - 3));
            double denominator = kurtosis + sampleCorrection;

            return numerator / denominator;
        }

        public static bool DetectSignificantGap(List<TimeTicks> sortedValues, double sd)
        {
            double maxGap = 0;
            for (int i = 0; i < sortedValues.Count - 1; i++)
            {
                double gap = (double)sortedValues[i + 1] - (double)sortedValues[i];
                if (gap > maxGap) maxGap = gap;
            }

            // A gap > 1.2 * SD usually indicates a 'valley' between two strat peaks
            return maxGap > (sd * 1.2);
        }

        public struct PeakMetrics
        {
            public TimeTicks Value;       // Time center of the peak
            public double Weight;      // % of total runs in this cluster
            public double Consistency; // How tight this cluster is (0.0 to 1.0)
            public int RunCount;       // Number of runs in this cluster
        }

        public struct PeakReport
        {
            public bool IsBimodal;
            public PeakMetrics FastPeak;
            public PeakMetrics SlowPeak;
            public string Summary;
        }

        public static PeakReport GetFullPeakAnalysis(List<TimeTicks> times, TimeTicks min, TimeTicks max,  bool bimodalDetected)
        {
            if (times == null || times.Count == 0) return new PeakReport();

            double range = (double)max - (double)min;

            // 1. Histogram / Peak Finding
            int binCount = 15;
            int[] bins = new int[binCount];
            foreach (var t in times)
            {
                int binIdx = (range == 0) ? 0 : (int)(((double)t - (double)min) / range * (binCount - 1));
                bins[binIdx]++;
            }

            var localMaxima = new List<(int index, int count)>();
            for (int i = 0; i < binCount; i++)
            {
                bool left = (i == 0) || (bins[i] >= bins[i - 1]);
                bool right = (i == binCount - 1) || (bins[i] >= bins[i + 1]);
                if (left && right && bins[i] > 0) localMaxima.Add((i, bins[i]));
            }

            // 2. Identify the Peak Centers
            double fastVal, slowVal;
            bool activeBimodal = bimodalDetected && localMaxima.Count >= 2;

            if (activeBimodal)
            {
                var topTwo = localMaxima.OrderByDescending(m => m.count).Take(2).OrderBy(m => m.index).ToList();
                fastVal = (double)min + (topTwo[0].index * (range / (binCount - 1)));
                slowVal = (double)min + (topTwo[1].index * (range / (binCount - 1)));
            }
            else
            {
                var (index, count) = localMaxima.OrderByDescending(m => m.count).FirstOrDefault();
                fastVal = slowVal = (double)min + (index * (range / (binCount - 1)));
            }

            // 3. Importance Calculation (Clustering)
            // Assign every run to the nearest peak to find Weight and Consistency
            var fastCluster = new List<double>();
            var slowCluster = new List<double>();

            foreach (var t in times)
            {
                if (!activeBimodal || Math.Abs((double)t - fastVal) <= Math.Abs((double)t - slowVal))
                    fastCluster.Add((double)t);
                else
                    slowCluster.Add((double)t);
            }

            // 4. Build Metrics
            var report = new PeakReport {
                IsBimodal = activeBimodal,
                FastPeak = CreateMetrics(fastCluster, fastVal, times.Count),
                SlowPeak = activeBimodal ? CreateMetrics(slowCluster, slowVal, times.Count) : CreateMetrics(fastCluster, fastVal, times.Count),
            };

            // 5. Sanity Check: If one peak is just a tiny outlier, downgrade to Unimodal
            double weightThreshold = 0.05; // 5% minimum weight to be considered a "Strat"
            if (report.IsBimodal)
            {
                if (report.FastPeak.Weight < weightThreshold || report.SlowPeak.Weight < weightThreshold)
                {
                    report.IsBimodal = false;
                    // Keep the "heavier" peak as the primary
                    var dominant = report.FastPeak.Weight >= report.SlowPeak.Weight ? report.FastPeak : report.SlowPeak;
                    report.FastPeak = dominant;
                    report.SlowPeak = dominant;
                }
            }

            report.Summary = GenerateNarrative(report);

            return report;
        }

        private static PeakMetrics CreateMetrics(List<double> cluster, double peakValue, int totalCount)
        {
            if (cluster.Count == 0) return new PeakMetrics();
            
            // Consistency: 1.0 is perfect, 0.0 is high variance. 
            // We use Mean Absolute Deviation (MAD) relative to the peak.
            double avgDev = cluster.Average(t => Math.Abs(t - peakValue));
            double consistency = Math.Max(0, 1.0 - (avgDev / (peakValue * 0.1))); // Penalty starts if dev > 10% of time

            return new PeakMetrics {
                Value = new TimeTicks((long)Math.Round(peakValue)),
                Weight = (double)cluster.Count / totalCount,
                Consistency = consistency,
                RunCount = cluster.Count
            };
        }

        private static string GenerateNarrative(PeakReport report)
        {
            if (!report.IsBimodal)
            {
                string consistencyDesc = report.FastPeak.Consistency > 0.8 ? "tight" : "loose";
                return $"Single execution mode at {report.FastPeak.Value}. Your playstyle is {consistencyDesc}.";
            }

            TimeTicks timeLoss = report.SlowPeak.Value - report.FastPeak.Value;
            int fastPercent = (int)(report.FastPeak.Weight * 100);

            // Logic for Interpretation
            string interpretation;
            if (report.FastPeak.Weight > 0.7)
                interpretation = "You've mostly mastered the main strat with occasional falls to backup.";
            else if (report.FastPeak.Weight < 0.3)
                interpretation = "You are primarily using a backup; the fast strat is currently a 'fluke'.";
            else
                interpretation = "You are currently split between your strat and a backup.";

            if (report.FastPeak.Consistency < report.SlowPeak.Consistency && report.FastPeak.Weight > 0.5)
                interpretation += " Warning: Your fast strat is significantly messier than your backup.";

            return $"Bimodal Detected. Fast: {report.FastPeak.Value} ({fastPercent}% weight {FormatPercent(report.FastPeak.Consistency)} consistency). " +
                $"Backup: {report.SlowPeak.Value}. Time loss: +{timeLoss}. {interpretation}";
        }
    }
}
