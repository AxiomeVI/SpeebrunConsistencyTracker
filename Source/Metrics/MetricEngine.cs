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

        // A metric's names as the settings it was computed under give them.
        public readonly record struct ComputedMetric(string CsvHeader, string OverlayName, MetricResult Result);

        public static List<ComputedMetric> Compute(PracticeSession session, MetricOutput mode)
            => Compute(session, mode, SpeebrunConsistencyTrackerModule.Settings);

        public static List<ComputedMetric> Compute(PracticeSession session, MetricOutput mode, SpeebrunConsistencyTrackerModuleSettings settings)
        {
            MetricContext context = new(TargetTimeTicksFor(settings), MetricHelper.ToInt(settings.PercentileValue));
            List<ComputedMetric> result = [];

            foreach (MetricDefinition metric in FilterMetrics(settings, mode))
            {
                result.Add(new ComputedMetric(
                    metric.CsvHeader(settings),
                    metric.OverlayName?.Invoke(settings),
                    metric.Compute(session, context, mode == MetricOutput.Export)));
            }

            return result;
        }

        private static int ComputeOverlaySettingsHash()
        {
            SpeebrunConsistencyTrackerModuleSettings settings = SpeebrunConsistencyTrackerModule.Settings;
            var hashCode = new HashCode();
            foreach (string header in FilterMetrics(settings, MetricOutput.Overlay).Select(m => m.CsvHeader(settings)))
                hashCode.Add(header);
            return hashCode.ToHashCode();
        }

        private static IEnumerable<MetricDefinition> FilterMetrics(SpeebrunConsistencyTrackerModuleSettings settings, MetricOutput mode)
            => MetricTable.All.Where(m => m.IsEnabled(settings, mode));

        public static TimeTicks GetTargetTimeTicks() => TargetTimeTicksFor(SpeebrunConsistencyTrackerModule.Settings);

        public static TimeTicks TargetTimeTicksFor(SpeebrunConsistencyTrackerModuleSettings settings)
        {
            int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
            return new TimeTicks(TimeSpan.FromMilliseconds(totalMilliseconds).Ticks);
        }
    }
}
