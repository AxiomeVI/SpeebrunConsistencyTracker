using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Globalization;

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
            switch (value)
            {
                case bool b:
                    return b;
                case MetricOutputChoice choice:
                    return (FromChoice(choice) & mode) != 0;
                default:
                    throw new InvalidOperationException($"Unsupported type {value.GetType()}");
            }
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
    }
}
