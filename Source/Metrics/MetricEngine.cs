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
        public static List<(MetricDescriptor, MetricResult)> Compute(PracticeSession session, MetricOutput mode)
        {
            MetricContext context = new MetricContext();
            List<(MetricDescriptor, MetricResult)> result = new List<(MetricDescriptor, MetricResult)>();

            foreach (MetricDescriptor metric in FilterMetrics(mode))
            {
                result.Add((metric, metric.Compute(session, context, mode == MetricOutput.Export)));
            }

            return result;
        }

        private static List<MetricDescriptor> FilterMetrics(MetricOutput mode)
        {
            List<MetricDescriptor> filteredMetrics = MetricRegistry.AllMetrics.Where(m => m.IsEnabled(mode)).ToList();
            // foreach (MetricDescriptor metric in MetricRegistry.AllMetrics)
            // {
            //     PropertyInfo prop = SpeebrunConsistencyTrackerModule.Settings.StatsMenu.GetType().GetProperty(metric.Key);
            //     Logger.Log(LogLevel.Info, "SpeebrunConsistencyTracker", prop.ToString());
            //     object value = prop.GetValue(SpeebrunConsistencyTrackerModule.Settings.StatsMenu);
            //     Logger.Log(LogLevel.Info, "SpeebrunConsistencyTracker", value.ToString());
            //     if (IsMetricEnabled(value, mode))
            //     {
            //         Logger.Log(LogLevel.Info, "SpeebrunConsistencyTracker", "COUCOU");
            //         filteredMetrics.Add(metric);
            //     }
            // }
            Logger.Log(LogLevel.Info, "SpeebrunConsistencyTracker", filteredMetrics.Count.ToString());
            return filteredMetrics;
        }

        public static TimeTicks GetTargetTimeTicks() {
            var settings = SpeebrunConsistencyTrackerModule.Settings.TargetTime;
            int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
            return new TimeTicks(TimeSpan.FromMilliseconds(totalMilliseconds).Ticks);
        }
    }
}
