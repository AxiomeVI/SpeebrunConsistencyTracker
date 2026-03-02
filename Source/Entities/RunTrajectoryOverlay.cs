using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Line chart showing each attempt as a trajectory relative to the room average baseline.
    /// Lines go up when a room is faster than average, down when slower.
    /// Deviation is cumulative across rooms.
    /// Attempts are ordered: regulars (chronological) -> best (if not last) -> last.
    /// If best == last, it appears only once at the end in gold.
    /// </summary>
    public class RunTrajectoryOverlay : BaseChartOverlay
    {
        private readonly int _totalRooms;

        private record AttemptLine(
            long[] CumulativeDeviations,
            int RoomsCompleted);

        // Ordered: regulars (chronological) -> best (if not last) -> last
        private readonly List<AttemptLine> _attempts;
        private readonly AttemptLine _sobLine;
        private readonly bool _lastIsBest;
        private readonly long _maxPositiveDeviation;
        private readonly long _maxNegativeDeviation;
        private readonly long _totalRange;

        public RunTrajectoryOverlay(
            IReadOnlyList<Attempt> attempts,
            List<List<TimeTicks>> roomTimes,
            int totalRooms,
            Vector2? pos = null)
            : base("Run Trajectory — deviation from average", pos)
        {
            _totalRooms = totalRooms;

            if (attempts.Count == 0)
            {
                _attempts = [];
                _sobLine = new AttemptLine([], 0);
                _maxPositiveDeviation = 1;
                _maxNegativeDeviation = 1;
                _totalRange = 2;
                return;
            }

            // Per-room averages from all attempts that reached each room
            long[] roomAverages = [.. Enumerable.Range(0, _totalRooms).Select(r =>
            {
                var times = attempts
                    .Where(a => a.CompletedRooms.Count > r)
                    .Select(a => a.CompletedRooms[r].Ticks)
                    .ToList();
                return times.Count == 0 ? 0L : (long)times.Average();
            })];

            // Build raw attempt lines in chronological order
            var raw = attempts.Select(a =>
            {
                long cumulative = 0;
                var deviations = new List<long>();
                for (int r = 0; r < a.CompletedRooms.Count && r < _totalRooms; r++)
                {
                    cumulative += roomAverages[r] - a.CompletedRooms[r].Ticks; // positive = faster
                    deviations.Add(cumulative);
                }
                return (line: new AttemptLine([.. deviations], deviations.Count), finalDeviation: deviations.Count > 0 ? deviations[^1] : 0L);
            }).ToList();

            // Find best: full runs first, then most positive final deviation
            int bestIdx = Enumerable.Range(0, raw.Count)
                .Where(i => raw[i].line.RoomsCompleted == _totalRooms)
                .OrderByDescending(i => raw[i].finalDeviation)
                .FirstOrDefault(-1);

            // If no runs are completed, default back to uncompleted ones
            if (bestIdx < 0)
                bestIdx = Enumerable.Range(0, raw.Count)
                    .OrderByDescending(i => raw[i].line.RoomsCompleted)
                    .ThenByDescending(i => raw[i].finalDeviation)
                    .First();

            int lastIdx = raw.Count - 1;
            _lastIsBest = bestIdx == lastIdx;

            // Arrange: regulars -> best (if not last) -> last
            var regulars = Enumerable.Range(0, raw.Count)
                .Where(i => i != bestIdx && i != lastIdx)
                .Select(i => raw[i].line);

            _attempts = [.. regulars];
            if (!_lastIsBest)
                _attempts.Add(raw[bestIdx].line);
            _attempts.Add(raw[lastIdx].line);

            // Compute SoB cumulative deviations
            long sobCumulative = 0;
            var sobDeviations = new long[_totalRooms];
            for (int r = 0; r < _totalRooms; r++)
            {
                var times = r < roomTimes.Count ? roomTimes[r] : [];
                long sob = times.Count > 0 ? times.Min(t => t.Ticks) : roomAverages[r];
                sobCumulative += roomAverages[r] - sob;
                sobDeviations[r] = sobCumulative;
            }
            _sobLine = new AttemptLine(sobDeviations, _totalRooms);

            // SoB is always the most positive deviation by definition
            _maxPositiveDeviation = Math.Max(sobDeviations[^1], 1);

            // Max negative only needs to come from attempts
            _maxNegativeDeviation = Math.Max(
                _attempts
                    .SelectMany(a => a.CumulativeDeviations)
                    .Where(d => d < 0)
                    .Select(d => -d)
                    .DefaultIfEmpty(1)
                    .Max(), 1);

            _totalRange = _maxPositiveDeviation + _maxNegativeDeviation;
        }

        protected override void DrawAxes(float x, float y, float w, float h)
        {
            // X axis
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            // Y axis
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, 2f);
            // Baseline (zero deviation) — positioned proportionally
            float baselineY = y + (float)_maxPositiveDeviation / _totalRange * h;
            Draw.Line(new Vector2(x, baselineY), new Vector2(x + w, baselineY), Color.Gray * 0.6f, 1f);
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_attempts.Count == 0) return;

            float columnWidth = w / _totalRooms;
            float baselineY = y + (float)_maxPositiveDeviation / _totalRange * h;
            float scale = h / _totalRange;
            int total = _attempts.Count;

            int regularCount = _lastIsBest ? total - 1 : total - 2;
            for (int i = 0; i < regularCount; i++)
            {
                float brightness = total <= 1 ? 0.8f : MathHelper.Lerp(0.1f, 0.8f, (float)i / (total - 1));
                DrawAttemptLine(_attempts[i], x, baselineY, columnWidth, scale, h, Color.White * brightness, 1.5f);
            }

            // SoB line
            DrawAttemptLine(_sobLine, x, baselineY, columnWidth, scale, h, Color.LightBlue, 2f);

            if (!_lastIsBest)
            {
                DrawAttemptLine(_attempts[^2], x, baselineY, columnWidth, scale, h, Color.Gold, 2f);
                DrawAttemptLine(_attempts[^1], x, baselineY, columnWidth, scale, h, Color.Red, 2f);
            }
            else
            {
                DrawAttemptLine(_attempts[^1], x, baselineY, columnWidth, scale, h, Color.Gold, 2f);
            }
        }

        private static void DrawAttemptLine(AttemptLine attempt, float x, float baselineY, float columnWidth, float scale, float h, Color color, float thickness)
        {
            for (int r = 0; r < attempt.RoomsCompleted; r++)
            {
                float x1 = x + r * columnWidth;
                float y1 = r == 0
                    ? baselineY
                    : baselineY - attempt.CumulativeDeviations[r - 1] * scale;
                float x2 = x + (r + 1) * columnWidth;
                float y2 = baselineY - attempt.CumulativeDeviations[r] * scale;

                y1 = MathHelper.Clamp(y1, baselineY - h, baselineY + h);
                y2 = MathHelper.Clamp(y2, baselineY - h, baselineY + h);

                Draw.Line(new Vector2(x1, y1), new Vector2(x2, y2), color, thickness);
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            DrawTitle();

            float columnWidth = w / _totalRooms;
            float baselineY = y + (float)_maxPositiveDeviation / _totalRange * h;

            // X axis room labels + vertical grid lines
            float baseLabelY = y + h + 10;
            for (int r = 0; r < _totalRooms; r++)
            {
                string label = $"R{r + 1}";
                float labelX = x + r * columnWidth + columnWidth / 2f;
                Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
                float labelY = _totalRooms > 25 ? r % 2 == 0 ? baseLabelY : baseLabelY + 20 : baseLabelY;

                ActiveFont.DrawOutline(label,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    Vector2.Zero, Vector2.One * 0.35f,
                    Color.LightGray, 2f, Color.Black);

                Draw.Line(
                    new Vector2(x + r * columnWidth, y),
                    new Vector2(x + r * columnWidth, y + h),
                    Color.Gray * 0.5f, 1f);
            }

            // Y axis tick labels — proportional above/below baseline
            float aboveHeight = (float)_maxPositiveDeviation / _totalRange * h;
            float belowHeight = (float)_maxNegativeDeviation / _totalRange * h;
            int totalTicks = 12;
            int ticksAbove = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow = Math.Max(1, totalTicks - ticksAbove);

            DrawYTicks(x, y, w, baselineY, aboveHeight, _maxPositiveDeviation, ticksAbove, upward: true);
            DrawYBaseline(x, baselineY);
            DrawYTicks(x, y, w, baselineY, belowHeight, _maxNegativeDeviation, ticksBelow, upward: false);

            // Legend
            float legendY = y + h + 55;
            float legendX = x + w;
            DrawLegendEntry(legendX, legendY, "Last run", Color.Red, 0.35f, right: true);
            float offset = ActiveFont.Measure("Last run").X * 0.35f + 40;
            DrawLegendEntry(legendX - offset, legendY, "Best run", Color.Gold, 0.35f, right: true);
            offset += ActiveFont.Measure("Best run").X * 0.35f + 40;
            DrawLegendEntry(legendX - offset, legendY, "SoB", Color.LightBlue, 0.35f, right: true);

            // Attempt count
            string stats = $"Attempts: {_attempts.Count}";
            Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
            ActiveFont.DrawOutline(stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + 55),
                Vector2.Zero, Vector2.One * 0.4f,
                Color.LightGray, 2f, Color.Black);
        }

        private static void DrawYTicks(float x, float y, float w, float baselineY, float sideHeight, long maxDeviation, int tickCount, bool upward)
        {
            string prefix = upward ? "-" : "+";
            for (int i = 1; i <= tickCount; i++)
            {
                long tickDeviation = maxDeviation / tickCount * i;
                float yPos = upward
                    ? baselineY - (float)i / tickCount * sideHeight
                    : baselineY + (float)i / tickCount * sideHeight;

                if (upward ? yPos < y : yPos > y + sideHeight + (baselineY - y)) continue;

                string timeLabel = prefix + new TimeTicks(tickDeviation).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.3f;
                ActiveFont.DrawOutline(timeLabel, new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * 0.3f, Color.White, 2f, Color.Black);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }
        }

        private static void DrawYBaseline(float x, float baselineY)
        {
            string timeLabel = "±0";
            Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.3f;
            ActiveFont.DrawOutline(timeLabel, new Vector2(x - labelSize.X - 10, baselineY - labelSize.Y / 2),
                Vector2.Zero, Vector2.One * 0.3f, Color.Gray, 2f, Color.Black);
        }
    }
}