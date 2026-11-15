using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Microsoft.Xna.Framework;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The scatter's hit test looks only at the dots near the mouse in each column. It must name
// the dot the scan of every dot named: the nearest within the radius, the earliest of equals.
public class ScatterHitTests
{
    private const float Radius = 8f;

    // The scan the chart made before: every dot, in order.
    private static int Scan(float[] x, float[] y, int count, int firstSegment, Vector2 room, Vector2 segment, Vector2 mouse, Func<int, bool> shows)
    {
        float best = Radius * Radius;
        int found = -1;
        for (int i = 0; i < count; i++)
        {
            Vector2 by = i >= firstSegment ? segment : room;
            float dx = x[i] + by.X - mouse.X, dy = y[i] + by.Y - mouse.Y, distance = dx * dx + dy * dy;
            if (distance > best || (distance == best && found >= 0) || !shows(i)) continue;
            best  = distance;
            found = i;
        }
        return found;
    }

    // Columns of dots spread left to right as the chart lays them out, some sharing an x, the last
    // one the segment's.
    private static (float[] X, float[] Y, int[] Starts, int Columns, int FirstSegment) Columns(Random random, int columns, int perColumn)
    {
        List<float> x = [], y = [];
        int[] starts = new int[columns + 1];
        for (int c = 0; c < columns; c++)
        {
            starts[c] = x.Count;
            int count = random.Next(0, perColumn + 1);
            float centre = 100f + 60f * c;
            for (int i = 0; i < count; i++)
            {
                // Rounded to quarter pixels so that equal x and equal distances happen.
                x.Add(MathF.Round(4f * (count <= 1 ? centre : centre + ((float)i / (count - 1) - 0.5f) * 40f)) / 4f);
                y.Add(MathF.Round(4f * (200f + (float)random.NextDouble() * 60f)) / 4f);
            }
        }
        starts[columns] = x.Count;
        return ([.. x], [.. y], starts, columns, starts[columns - 1]);
    }

    [Fact]
    public void It_names_the_dot_a_scan_of_every_dot_names()
    {
        var random = new Random(35);
        for (int round = 0; round < 40; round++)
        {
            (float[] x, float[] y, int[] starts, int columns, int firstSegment) = Columns(random, 1 + random.Next(8), 60);
            Vector2 room = new(-MathF.Round((float)random.NextDouble() * 50f), (float)random.NextDouble() * 10f - 5f);
            Vector2 segment = new(0f, (float)random.NextDouble() * 10f - 5f);
            bool[] hidden = new bool[x.Length];
            for (int i = 0; i < hidden.Length; i++) hidden[i] = random.Next(10) == 0;
            Func<int, bool> shows = i => !hidden[i];
            for (int probe = 0; probe < 400; probe++)
            {
                Vector2 mouse = new(MathF.Round(4f * (40f + (float)random.NextDouble() * 60f * columns)) / 4f,
                                    MathF.Round(4f * (190f + (float)random.NextDouble() * 80f)) / 4f);
                Assert.Equal(Scan(x, y, x.Length, firstSegment, room, segment, mouse, shows),
                             ScatterChart.Nearest(x, y, starts, columns, firstSegment, room, segment, mouse, Radius, shows));
            }
        }
    }

    [Fact]
    public void Of_two_dots_as_near_the_earlier_is_named()
    {
        float[] x = [10f, 10f, 10f], y = [5f, 15f, 15f];
        int[] starts = [0, 3];
        Assert.Equal(1, ScatterChart.Nearest(x, y, starts, 1, 3, Vector2.Zero, Vector2.Zero, new Vector2(10f, 15f), Radius, _ => true));
        Assert.Equal(0, ScatterChart.Nearest(x, y, starts, 1, 3, Vector2.Zero, Vector2.Zero, new Vector2(10f, 10f), Radius, _ => true));
    }

    [Fact]
    public void A_dot_that_does_not_show_is_passed_over()
    {
        float[] x = [10f, 12f], y = [10f, 10f];
        int[] starts = [0, 2];
        Assert.Equal(1, ScatterChart.Nearest(x, y, starts, 1, 2, Vector2.Zero, Vector2.Zero, new Vector2(10f, 10f), Radius, i => i != 0));
    }

    [Fact]
    public void A_scrolled_room_is_hit_where_it_shows()
    {
        // The room's dot was built at 100 and shows at 70; the segment's does not scroll.
        float[] x = [100f, 100f], y = [10f, 10f];
        int[] starts = [0, 1, 2];
        Vector2 room = new(-30f, 0f), segment = Vector2.Zero;
        Assert.Equal(0, ScatterChart.Nearest(x, y, starts, 2, 1, room, segment, new Vector2(70f, 10f), Radius, _ => true));
        Assert.Equal(1, ScatterChart.Nearest(x, y, starts, 2, 1, room, segment, new Vector2(100f, 10f), Radius, _ => true));
    }
}
