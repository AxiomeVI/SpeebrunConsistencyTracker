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
        private static List<MetricDescriptor> lastFilter = [];

        public static List<(MetricDescriptor, MetricResult)> Compute(PracticeSession session, MetricOutput mode)
        {
            MetricContext context = new MetricContext();
            List<(MetricDescriptor, MetricResult)> result = new List<(MetricDescriptor, MetricResult)>();

            List<MetricDescriptor> filteredMetrics = FilterMetrics(mode);
            if (mode == MetricOutput.Overlay) lastFilter = filteredMetrics;

            foreach (MetricDescriptor metric in FilterMetrics(mode))
            {
                result.Add((metric, metric.Compute(session, context, mode == MetricOutput.Export)));
            }

            return result;
        }

        private static List<MetricDescriptor> FilterMetrics(MetricOutput mode)
        {
            List<MetricDescriptor> filteredMetrics = MetricRegistry.AllMetrics.Where(m => m.IsEnabled(mode)).ToList();
            return filteredMetrics;
        }

        public static bool SameSettings()
        {
            return lastFilter.SequenceEqual(FilterMetrics(MetricOutput.Overlay));
        }

        public static TimeTicks GetTargetTimeTicks() {
            var settings = SpeebrunConsistencyTrackerModule.Settings;
            int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
            return new TimeTicks(TimeSpan.FromMilliseconds(totalMilliseconds).Ticks);
        }
    }
}
