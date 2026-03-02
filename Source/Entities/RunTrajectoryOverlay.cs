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
    /// Gradient from oldest (faded) to newest (bright).
    /// Best attempt shown in gold, most recent in white.
    /// </summary>
    public class RunTrajectoryOverlay : BaseChartOverlay
    {
        private readonly int _totalRooms;

        // Precomputed attempt data
        private record AttemptLine(
            long[] CumulativeDeviations,
            int RoomsCompleted,
            long FinalDeviation,
            bool IsBest,
            bool IsLast);

        private readonly List<AttemptLine> _attempts;
        private readonly long _maxPositiveDeviation; // max above baseline (faster)
        private readonly long _maxNegativeDeviation; // max below baseline (slower, stored as positive)
        private readonly long _totalRange;
        private readonly long[] _sobDeviations;

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

            // Build attempt lines
            var rawAttempts = attempts.Select(a =>
            {
                long cumulative = 0;
                var deviations = new List<long>();

                for (int r = 0; r < a.CompletedRooms.Count && r < _totalRooms; r++)
                {
                    cumulative += roomAverages[r] - a.CompletedRooms[r].Ticks; // positive = faster
                    deviations.Add(cumulative);
                }

                return (deviations, roomsCompleted: deviations.Count);
            }).ToList();

            // Best attempt — full runs first, then most positive final deviation
            int bestIdx = Enumerable.Range(0, rawAttempts.Count)
                .Where(i => rawAttempts[i].roomsCompleted == _totalRooms)
                .OrderByDescending(i => rawAttempts[i].deviations.LastOrDefault())
                .FirstOrDefault(-1);

            if (bestIdx < 0)
                bestIdx = Enumerable.Range(0, rawAttempts.Count)
                    .OrderByDescending(i => rawAttempts[i].roomsCompleted)
                    .ThenByDescending(i => rawAttempts[i].deviations.LastOrDefault())
                    .First();

            int lastIdx = rawAttempts.Count - 1;

            _attempts = [.. rawAttempts.Select((a, i) => new AttemptLine(
                [.. a.deviations],
                a.roomsCompleted,
                a.deviations.Count > 0 ? a.deviations[^1] : 0,
                i == bestIdx,
                i == lastIdx))];

            // Compute SOB cumulative deviations
            long sobCumulative = 0;
            _sobDeviations = new long[_totalRooms];
            for (int r = 0; r < _totalRooms; r++)
            {
                var times = r < roomTimes.Count ? roomTimes[r] : [];
                long sob = times.Count > 0 ? times.Min(t => t.Ticks) : roomAverages[r];
                sobCumulative += roomAverages[r] - sob;
                _sobDeviations[r] = sobCumulative;
            }

            // SOB is always the most positive deviation by definition
            _maxPositiveDeviation = Math.Max(_sobDeviations[^1], 1);

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

            // Draw normal attempts first, then best, then last on top
            var drawOrder = Enumerable.Range(0, total)
                .Where(i => !_attempts[i].IsBest && !_attempts[i].IsLast)
                .Concat(Enumerable.Range(0, total).Where(i => _attempts[i].IsBest && !_attempts[i].IsLast))
                .Concat(Enumerable.Range(0, total).Where(i => _attempts[i].IsLast));

            foreach (int i in drawOrder)
            {
                AttemptLine attempt = _attempts[i];
                if (attempt.RoomsCompleted == 0) continue;

                // Age factor: 0 = oldest, 1 = newest
                float ageFactor = total <= 1 ? 1f : (float)i / (total - 1);

                Color lineColor;
                float thickness;

                if (attempt.IsBest)
                {
                    lineColor = Color.Gold;
                    thickness = 2f;
                }
                else if (attempt.IsLast)
                {
                    lineColor = Color.Red;
                    thickness = 2f;
                }
                else
                {
                    // Grey gradient — oldest dark, newest bright
                    float brightness = MathHelper.Lerp(0.1f, 0.8f, ageFactor);
                    lineColor = Color.White * brightness;
                    thickness = 1.5f;
                }

                // Draw line segments
                for (int r = 0; r < attempt.RoomsCompleted; r++)
                {
                    float x1 = x + r * columnWidth;
                    float y1 = r == 0
                        ? baselineY
                        : baselineY - attempt.CumulativeDeviations[r - 1] * scale;
                    float x2 = x + (r + 1) * columnWidth;
                    float y2 = baselineY - attempt.CumulativeDeviations[r] * scale;

                    // Clamp to graph bounds
                    y1 = MathHelper.Clamp(y1, y, y + h);
                    y2 = MathHelper.Clamp(y2, y, y + h);

                    Draw.Line(new Vector2(x1, y1), new Vector2(x2, y2), lineColor, thickness);
                }
            }

            // Draw SOB line — above normal lines, below last and best
            if (_sobDeviations.Length > 0)
            {
                for (int r = 0; r < _sobDeviations.Length; r++)
                {
                    float x1 = x + r * columnWidth;
                    float y1 = r == 0
                        ? baselineY
                        : baselineY - _sobDeviations[r - 1] * scale;
                    float x2 = x + (r + 1) * columnWidth;
                    float y2 = baselineY - _sobDeviations[r] * scale;

                    y1 = MathHelper.Clamp(y1, y, y + h);
                    y2 = MathHelper.Clamp(y2, y, y + h);

                    Draw.Line(new Vector2(x1, y1), new Vector2(x2, y2), Color.Turquoise, 2f);
                }
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            DrawTitle();

            float columnWidth = w / _totalRooms;
            float baselineY = y + (float)_maxPositiveDeviation / _totalRange * h;

            // X axis room labels
            float baseLabelY = y + h + 10;
            for (int r = 0; r < _totalRooms; r++)
            {
                string label = $"R{r + 1}";
                float labelX = x + r * columnWidth + columnWidth / 2f;
                Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
                float labelY = _totalRooms > 25 ? r % 2 == 0 ? baseLabelY : baseLabelY + 20 : baseLabelY;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.35f,
                    Color.LightGray, 2f, Color.Black);

                // Vertical grid line per room
                Draw.Line(
                    new Vector2(x + r * columnWidth, y),
                    new Vector2(x + r * columnWidth, y + h),
                    Color.Gray * 0.5f, 1f);
            }

            // Y axis labels — tick count proportional to space above/below baseline
            float aboveHeight = (float)_maxPositiveDeviation / _totalRange * h;
            float belowHeight = (float)_maxNegativeDeviation / _totalRange * h;
            int totalTicks = 12;
            int ticksAbove = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow = Math.Max(1, totalTicks - ticksAbove);

            // Above baseline (faster = negative time)
            for (int i = 1; i <= ticksAbove; i++)
            {
                long tickDeviation = _maxPositiveDeviation / ticksAbove * i;
                float yPos = baselineY - (float)i / ticksAbove * aboveHeight;
                if (yPos < y) continue;

                string timeLabel = "-" + new TimeTicks(tickDeviation).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.3f;
                ActiveFont.DrawOutline(timeLabel, new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * 0.3f, Color.White, 2f, Color.Black);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }

            // Baseline label
            {
                string timeLabel = "±0";
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.3f;
                ActiveFont.DrawOutline(timeLabel, new Vector2(x - labelSize.X - 10, baselineY - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * 0.3f, Color.Gray, 2f, Color.Black);
            }

            // Below baseline (slower = positive time)
            for (int i = 1; i <= ticksBelow; i++)
            {
                long tickDeviation = _maxNegativeDeviation / ticksBelow * i;
                float yPos = baselineY + (float)i / ticksBelow * belowHeight;
                if (yPos > y + h) continue;

                string timeLabel = "+" + new TimeTicks(tickDeviation).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.3f;
                ActiveFont.DrawOutline(timeLabel, new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * 0.3f, Color.White, 2f, Color.Black);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }

            // Legend
            float legendY = y + h + 55;
            float legendX = x + w;
            DrawLegendEntry(legendX, legendY, "Last run", Color.Red, 0.35f, right: true);
            float offset = ActiveFont.Measure("Last run").X * 0.35f + 40;
            DrawLegendEntry(legendX - offset, legendY, "Best run", Color.Gold, 0.35f, right: true);
            offset += ActiveFont.Measure("Best run").X * 0.35f + 40;
            DrawLegendEntry(legendX - offset, legendY, "SOB", Color.LightBlue, 0.35f, right: true);

            // Attempt count
            string stats = $"Attempts: {_attempts.Count}";
            Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
            ActiveFont.DrawOutline(
                stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + 55),
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                Color.LightGray, 2f, Color.Black);
        }
    }
}