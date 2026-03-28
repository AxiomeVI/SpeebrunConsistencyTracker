using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static class MetricEngine
    {
        private static int _settingsHash = 0;

        public static void Clear() { }

        public static void InvalidateSettingsHash()
        {
            _settingsHash = ComputeOverlaySettingsHash();
        }

        public static int GetOverlaySettingsHash() => _settingsHash;

        public static List<(MetricDescriptor, MetricResult)> Compute(PracticeSession session, MetricOutput mode)
        {
            MetricContext context = new();
            List<(MetricDescriptor, MetricResult)> result = [];

            foreach (MetricDescriptor metric in FilterMetrics(mode))
            {
                result.Add((metric, metric.Compute(session, context, mode == MetricOutput.Export)));
            }

            return result;
        }

        private static int ComputeOverlaySettingsHash()
        {
            var settings = SpeebrunConsistencyTrackerModule.Settings;
            var hashCode = new HashCode();
            foreach (string header in FilterMetrics(MetricOutput.Overlay).Select(m => m.CsvHeader()))
                hashCode.Add(header);
            hashCode.Add(GetTargetTimeTicks());
            hashCode.Add(settings.PercentileValue);
            return hashCode.ToHashCode();
        }

        private static List<MetricDescriptor> FilterMetrics(MetricOutput mode)
        {
            return [.. MetricRegistry.AllMetrics.Where(m => m.IsEnabled(mode))];
        }

        public static TimeTicks GetTargetTimeTicks()
        {
            var settings = SpeebrunConsistencyTrackerModule.Settings;
            int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
            return new TimeTicks(TimeSpan.FromMilliseconds(totalMilliseconds).Ticks);
        }
    }
}
