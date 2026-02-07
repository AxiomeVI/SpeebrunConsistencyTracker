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
        private readonly Dictionary<string, object> _cache = [];

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

    public sealed class MetricDescriptor(
        Func<string> csvHeader,
        Func<string> inGameName,
        Func<PracticeSession, MetricContext, bool, MetricResult> compute,
        Func<MetricOutput, bool> isEnabled) : IEquatable<MetricDescriptor>
    {
        public Func<string> CsvHeader { get; } = csvHeader ?? throw new ArgumentNullException(nameof(csvHeader));

        public Func<string> InGameName { get; } = inGameName ?? throw new ArgumentNullException(nameof(inGameName));

        public Func<PracticeSession, MetricContext, bool, MetricResult> Compute { get; } = compute ?? throw new ArgumentNullException(nameof(compute));

        public Func<MetricOutput, bool> IsEnabled { get; } = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));

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

    public sealed class MetricResult(string segmentValue, IReadOnlyList<string> roomValues)
    {
        public string SegmentValue { get; init; } = segmentValue ?? throw new ArgumentNullException(nameof(segmentValue));
        public IReadOnlyList<string> RoomValues { get; init; } = roomValues ?? throw new ArgumentNullException(nameof(roomValues));
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

            double median = ComputePercentile(sortedTimes, 50).Ticks;

            var deviations = sortedTimes
                .Select(t => (long)Math.Round(Math.Abs(t.Ticks - median)))
                .OrderBy(d => d);

            return ComputePercentile([.. deviations.Select(t => new TimeTicks(t))], 50);
        }

        public static double ComputeConsistencyScore_TOO_KIND(double median, TimeTicks min, double mad, double resetRate, double q1, double q3)
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

        public static double ComputeConsistencyScore(double median, TimeTicks min, double mad, double resetRate, double stdCV)
        {
            if (median <= 0) return 0;
            
            // 1. Stability: Use a Gaussian curve to provide a 'grace zone' for stability, followed by a sharp drop-off for high variance.
            double madCV = mad / median;
            double stabilityScore = Math.Exp(-50 * Math.Pow(madCV * stdCV, 2));

            // 2. Floor Proximity: How close is the median to the session pb?
            double gap = (median - (double)min) / median;
            double floorProximity = Math.Exp(-50 * Math.Pow(gap, 2));

            // 3. Reliability: Don't reset
            double completionRate = 1.0 - Math.Clamp(resetRate, 0, 1.0);
            double reliability = completionRate * completionRate; // Penalizes high reset rates heavily

            // FINAL CALCULATION: Multiplicative relationship
            // If ANY of these are low, the whole score crashes.
            double finalScore = stabilityScore * floorProximity * reliability;

            return Math.Clamp(finalScore, 0, 1.0);
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
            public TimeTicks Value;    // Time center of the peak
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

        public static PeakReport GetFullPeakAnalysis(List<TimeTicks> times, TimeTicks min, TimeTicks max, TimeTicks iqr, bool bimodalDetected)
        {
            if (times == null || times.Count == 0) return new PeakReport();

            const double ONE_FRAME = 170000; // one frame

            // 1. Determine how "precise" this segment needs to be
            // Use 10% of the min or the Freedman-Diaconis rule as a target for bin resolution
            double FreedmanDiaconis_width = 2 * iqr * Math.Pow(times.Count, -1.0 / 3.0);
            double heuristic_width = min * 0.1;
            // 2. Ensure we don't try to be more precise than the natural floor
            double binWidth = Math.Max(Math.Min(heuristic_width, FreedmanDiaconis_width), ONE_FRAME);
            double range = (double)max - (double)min;
            // 3. Calculate how many bins we need to cover the range at this resolution
            // We add a +1 and Clamp to ensure we have a valid array size
            int binCount = (int)Math.Ceiling(range / binWidth) + 1;
            binCount = Math.Clamp(binCount, 5, 50); // Keep it within sane limits for performance
            // 4. Re-calculate actual binWidth to perfectly fit the clamped count
            double finalBinWidth = (binCount > 1) ? range / (binCount - 1) : ONE_FRAME;

            // 1. Histogram / Peak Finding
            int[] bins = new int[binCount];
            foreach (var t in times)
            {
                int binIdx = (range <= 0) ? 0 : (int)Math.Floor((t - min) / finalBinWidth);
                bins[Math.Clamp(binIdx, 0, binCount - 1)]++;
            }

            var localMaxima = FindLocalMaxima(bins, times.Count);

            // 2. Identify the Peak Centers
            double fastVal, slowVal;
            bool activeBimodal = bimodalDetected && localMaxima.Count >= 2;
            if (activeBimodal)
            {
                // Take the two most significant peaks and sort them by time (index)
                var topTwo = localMaxima.OrderByDescending(m => m.count).Take(2).OrderBy(m => m.index).ToList();
                
                fastVal = GetRefinedPeak(topTwo[0].index, bins, min, binWidth);
                slowVal = GetRefinedPeak(topTwo[1].index, bins, min, binWidth);
            }
            else
            {
                // Fallback to the single highest peak
                var (index, count) = localMaxima.OrderByDescending(m => m.count).FirstOrDefault();
                fastVal = slowVal = GetRefinedPeak(index, bins, min, binWidth);
            }

            // 3. Clustering
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
                FastPeak = CreatePeakMetrics(fastCluster, fastVal, times.Count),
                SlowPeak = activeBimodal ? CreatePeakMetrics(slowCluster, slowVal, times.Count) : new PeakMetrics(),
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

        private static List<(int index, int count)> FindLocalMaxima(int[] bins, int totalCount)
        {
            var maxima = new List<(int, int)>();
            double noiseFloor = totalCount * 0.03;

            for (int i = 1; i < bins.Length - 1; i++)
            {
                if (bins[i] < noiseFloor) continue;

                if (bins[i] > bins[i - 1]) // Start of a hill
                {
                    int j = i;
                    while (j < bins.Length - 1 && bins[j + 1] == bins[i]) j++; // Handle plateaus

                    if (j < bins.Length - 1 && bins[i] > bins[j + 1]) // It drops off
                    {
                        maxima.Add(((i + j) / 2, bins[i]));
                        i = j;
                    }
                }
            }
            return maxima;
        }

        private static double GetRefinedPeak(int index, int[] bins, double min, double width)
        {
            double weightSum = bins[index];
            double indexSum = (double)index * bins[index];

            if (index > 0) { weightSum += bins[index - 1]; indexSum += (index - 1) * bins[index - 1]; }
            if (index < bins.Length - 1) { weightSum += bins[index + 1]; indexSum += (index + 1) * bins[index + 1]; }

            return min + (indexSum / weightSum * width);
        }

        private static PeakMetrics CreatePeakMetrics(List<double> cluster, double peakValue, int totalCount)
        {
            if (cluster.Count == 0) return new PeakMetrics();
            
            double standardDeviation = Math.Sqrt(cluster.Average(t => Math.Pow(t - peakValue, 2)));
            double cv = standardDeviation / peakValue;

            double consistency = Math.Exp(-50 * Math.Pow(cv, 2));

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
                return $"Single peak at {report.FastPeak.Value}.";
            }

            TimeTicks timeLoss = report.SlowPeak.Value - report.FastPeak.Value;

            return $"Bimodal Detected. Fast: {report.FastPeak.Value} ({FormatPercent(report.FastPeak.Weight)}% weight {FormatPercent(report.FastPeak.Consistency)} consistency). " +
                $"Backup: {report.SlowPeak.Value} ({FormatPercent(report.SlowPeak.Weight)}% weight {FormatPercent(report.SlowPeak.Consistency)} consistency). Time loss: +{timeLoss}.";
        }

        public static double CalculatePearson(List<double> x, List<double> y)
        {
            int n = x.Count;
            double avgX = x.Average();
            double avgY = y.Average();

            double sumXY = 0, sumX2 = 0, sumY2 = 0;

            for (int i = 0; i < n; i++)
            {
                double dX = x[i] - avgX;
                double dY = y[i] - avgY;
                sumXY += dX * dY;
                sumX2 += dX * dX;
                sumY2 += dY * dY;
            }

            double denominator = Math.Sqrt(sumX2 * sumY2);
            return (denominator == 0) ? 0 : sumXY / denominator;
        }
    }
}
