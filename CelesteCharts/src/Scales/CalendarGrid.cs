#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A practice calendar: a cell per day, weeks across (Monday first, as <see cref="Dates"/> counts weeks) and
///     the seven weekdays down, from a first day to a last. Days before the first or after the last in their
///     week have no cell. The same object gives a day's cell and the day under a pixel.
/// </summary>
// A CellGrid underneath, so a calendar colours and hovers like a heatmap. Dates are compared by day alone;
// the time of day is dropped.
internal sealed class CalendarGrid {
    public CalendarGrid(DateTime first, DateTime last, Area area, float fill = 0.85f) {
        if (last < first) (first, last) = (last, first);
        First = first.Date;
        Last = last.Date;
        FirstMonday = First.AddDays(-(((int) First.DayOfWeek + 6) % 7));
        Weeks = (int) ((Last - FirstMonday).TotalDays / 7) + 1;
        // Square cells: a column as wide as a row is tall, the grid at the area's left.
        float side = MathF.Floor(Math.Min(area.Width / Weeks, area.Height / 7f));
        Grid = new CellGrid(new Bands(Weeks, area.X, side * Weeks, fill, wholePixels: true),
                            new Bands(7, area.Y, side * 7f, fill, wholePixels: true));
    }

    public DateTime First { get; }
    public DateTime Last { get; }

    /// <summary>The Monday of the first week: column 0.</summary>
    public DateTime FirstMonday { get; }

    public int Weeks { get; }

    public CellGrid Grid { get; }

    /// <summary>Whether a day is between the first and the last.</summary>
    public bool Holds(DateTime day) => day.Date >= First && day.Date <= Last;

    /// <summary>The day in a column and row (Monday is row 0), whether or not the calendar holds it.</summary>
    public DateTime DayOf(int column, int row) => FirstMonday.AddDays(column * 7 + row);

    /// <summary>A day's cell; only meaningful for a day the calendar <see cref="Holds"/>.</summary>
    public Area Cell(DateTime day) {
        int days = (int) (day.Date - FirstMonday).TotalDays;
        return Grid.Cell(days / 7, days % 7);
    }

    /// <summary>The day under a pixel, or null off the calendar's days.</summary>
    public DateTime? DayAt(float x, float y) {
        (int column, int row) = Grid.IndexAt(x, y);
        if (column < 0) return null;
        DateTime day = DayOf(column, row);
        return Holds(day) ? day : null;
    }

    /// <summary>Each month that starts in the calendar (the first month too), with the column its first held day is in.</summary>
    public IReadOnlyList<(int Column, DateTime Month)> MonthStarts() {
        List<(int, DateTime)> starts = new();
        for (DateTime month = new(First.Year, First.Month, 1); month <= Last; month = month.AddMonths(1)) {
            DateTime from = month < First ? First : month;
            starts.Add(((int) ((from - FirstMonday).TotalDays / 7), month));
        }
        return starts;
    }

    /// <summary>English month abbreviations in the invariant culture; a mod passes its own to follow the game's language.</summary>
    public static string EnglishMonth(DateTime month) => month.ToString("MMM", CultureInfo.InvariantCulture);
}
