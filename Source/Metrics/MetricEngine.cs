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

        public static void Clear() { _settingsHash = 0; }

        public static void InvalidateSettingsHash()
        {
            _settingsHash = ComputeOverlaySettingsHash();
        }

        // What the overlay text was computed from. Only the metric toggles are a stored hash that a
        // menu must refresh; the target and the percentile are read live, so a path that changes
        // them (clipboard import, a drag of the target line) cannot leave the overlay stale.
        public readonly record struct OverlayKey(int Toggles, long TargetTicks, PercentileChoice Percentile);

        public static OverlayKey GetOverlayKey() => OverlayKeyFor(SpeebrunConsistencyTrackerModule.Settings, _settingsHash);

        public static OverlayKey OverlayKeyFor(SpeebrunConsistencyTrackerModuleSettings settings, int toggles)
            => new(toggles, TargetTimeTicksFor(settings).Ticks, settings.PercentileValue);

        public static List<(MetricDescriptor, MetricResult)> Compute(PracticeSession session, MetricOutput mode)
        {
            MetricContext context = new(GetTargetTimeTicks(), MetricHelper.ToInt(SpeebrunConsistencyTrackerModule.Settings.PercentileValue));
            List<(MetricDescriptor, MetricResult)> result = [];

            foreach (MetricDescriptor metric in FilterMetrics(mode))
            {
                result.Add((metric, metric.Compute(session, context, mode == MetricOutput.Export)));
            }

            return result;
        }

        private static int ComputeOverlaySettingsHash()
        {
            var hashCode = new HashCode();
            foreach (string header in FilterMetrics(MetricOutput.Overlay).Select(m => m.CsvHeader()))
                hashCode.Add(header);
            return hashCode.ToHashCode();
        }

        private static List<MetricDescriptor> FilterMetrics(MetricOutput mode)
        {
            return [.. MetricRegistry.AllMetrics.Where(m => m.IsEnabled(mode))];
        }

        public static TimeTicks GetTargetTimeTicks() => TargetTimeTicksFor(SpeebrunConsistencyTrackerModule.Settings);

        public static TimeTicks TargetTimeTicksFor(SpeebrunConsistencyTrackerModuleSettings settings)
        {
            int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
            return new TimeTicks(TimeSpan.FromMilliseconds(totalMilliseconds).Ticks);
        }
    }
}
