using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Microsoft.Xna.Framework;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// #69: what the player pinned and hid on a chart survives a pause, a toggle and a visit to another
// chart while the session holds. Each chart keeps its own. A session change, or a move of the
// segment's first room, still starts the chart with nothing.
public class ChartViewMemoryTests
{
    private const int NextRoom = 1, CurrentRoom = 2;

    // Marks every key in `shown`; any other key names no mark.
    private sealed class Chart : BaseChartOverlay
    {
        public readonly HashSet<string> Shown = [];
        public Chart(params string[] shown) : base("test") => Shown.UnionWith(shown);
        public override void Render() { }
        internal override ChartHelp Help => default;
        public override HoverInfo Locate(string key) => Shown.Contains(key) ? Pin(key, mark: 0) : null;
    }

    private static HoverInfo Pin(string key, int mark = 0) => new(key, Vector2.Zero, key, mark);

    private readonly PracticeSession _session = new(new StubSegmentShape());

    private static ChartCache<Chart> Cache(params string[] shown) =>
        new(_ => new Chart(shown), static (previous, fresh, renumbered) => fresh.Replace(previous, renumbered));

    [Fact]
    public void A_hide_keeps_the_pins_and_the_hidden_rooms()
    {
        ChartCache<Chart> cache = Cache("seg:3");
        Chart chart = cache.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        chart.ToggleColumn(2);
        chart.Pins.Toggle(Pin("seg:3"));

        GraphInteractivity.Leave(chart);
        Chart shownAgain = cache.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);

        Assert.Same(chart, shownAgain);
        Assert.Equal([2], shownAgain.HiddenRooms);
        Assert.Equal(["seg:3"], Keys(shownAgain));
    }

    // Each chart type has a cache, and a chart of its own: a pin on one is not on another.
    [Fact]
    public void Each_chart_keeps_its_own_view()
    {
        ChartCache<Chart> scatter = Cache("seg:3"), box = Cache("box:1");
        Chart a = scatter.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        a.Pins.Toggle(Pin("seg:3"));
        a.ToggleColumn(1);
        GraphInteractivity.Leave(a);

        Chart b = box.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        b.Pins.Toggle(Pin("box:1"));
        GraphInteractivity.Leave(b);

        Assert.Equal(["seg:3"], Keys(scatter.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1)));
        Assert.Equal(["box:1"], Keys(box.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1)));
        Assert.Empty(b.HiddenRooms);
    }

    // A room finished while the chart was hidden: the chart is rebuilt when it shows again.
    [Fact]
    public void A_chart_rebuilt_while_hidden_takes_the_pins_and_the_hidden_rooms()
    {
        ChartCache<Chart> cache = Cache("seg:3");
        Chart before = cache.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        before.ToggleColumn(2);
        before.Pins.Toggle(Pin("seg:3"));
        GraphInteractivity.Leave(before);

        Chart after = cache.Get(_session, 2, 5, NextRoom, curStartRoomIndex: 1);

        Assert.NotSame(before, after);
        Assert.Equal([2], after.HiddenRooms);
        Assert.Equal(["seg:3"], Keys(after));
    }

    // F10's switch to the current room's timer moves the first room: a room index names another room.
    [Fact]
    public void A_move_of_the_first_room_while_hidden_drops_the_pins_and_the_hidden_rooms()
    {
        ChartCache<Chart> cache = Cache("seg:3");
        Chart before = cache.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        before.ToggleColumn(2);
        before.Pins.Toggle(Pin("seg:3"));
        GraphInteractivity.Leave(before);

        Chart after = cache.Get(_session, 1, 5, CurrentRoom, curStartRoomIndex: 0);

        Assert.Empty(after.HiddenRooms);
        Assert.Empty(after.Pins.Items);
        Assert.True(after.TakeDroppedPins());
    }

    [Fact]
    public void Another_session_starts_the_chart_with_no_pins()
    {
        ChartCache<Chart> cache = Cache("seg:3");
        Chart before = cache.Get(_session, 1, 5, NextRoom, curStartRoomIndex: 1);
        before.Pins.Toggle(Pin("seg:3"));
        GraphInteractivity.Leave(before);

        Chart after = cache.Get(new PracticeSession(new StubSegmentShape()), 1, 5, NextRoom, curStartRoomIndex: 1);

        Assert.Empty(after.Pins.Items);
    }

    // The clear hotkey and the menu's clear: GraphManager.Clear.
    [Fact]
    public void A_clear_drops_the_pins_and_the_hidden_rooms()
    {
        Chart chart = new("seg:3");
        chart.ToggleColumn(2);
        chart.Pins.Toggle(Pin("seg:3"));

        GraphInteractivity.Clear(chart);

        Assert.Empty(chart.Pins.Items);
        Assert.Empty(chart.HiddenRooms);
    }

    // A Delete of the pinned cell while the chart is hidden, or a rebuilt chart with no such mark.
    [Fact]
    public void A_carried_pin_whose_mark_is_gone_is_dropped()
    {
        Chart before = new("seg:3", "seg:4");
        before.Pins.Toggle(Pin("seg:3"));
        before.Pins.Toggle(Pin("seg:4"));
        Chart after = new("seg:4");

        after.Replace(before, renumbered: false);
        after.Pins.Locate(after.Revision, after.Locate);

        Assert.Equal(["seg:4"], Keys(after));
    }

    // The same chart, scrolled or zoomed so the mark is out of the plot: the pin waits for it.
    [Fact]
    public void A_pin_on_the_same_chart_whose_mark_does_not_show_is_kept()
    {
        ChartPins pins = new();
        pins.Toggle(Pin("seg:3"));
        pins.Locate(0, key => Pin(key, mark: 7));

        pins.Locate(1, _ => null);

        HoverInfo kept = Assert.Single(pins.Items);
        Assert.Equal(-1, kept.Mark);
        Assert.False(pins.Locate(1, _ => null));
    }

    [Fact]
    public void A_second_click_on_a_pinned_mark_unpins_it()
    {
        ChartPins pins = new();
        pins.Toggle(Pin("seg:3"));
        pins.Toggle(Pin("seg:3", mark: 5));

        Assert.Empty(pins.Items);
    }

    private static List<string> Keys(BaseChartOverlay chart)
    {
        List<string> keys = [];
        foreach (HoverInfo pin in chart.Pins.Items) keys.Add(pin.Key);
        return keys;
    }
}
