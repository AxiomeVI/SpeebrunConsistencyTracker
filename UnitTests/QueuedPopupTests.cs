using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The popup a save shows waits for SpeedrunTool's own "Save to [slot]": both are drawn at the same
// spot, and shown together the second covers the first.
public class QueuedPopupTests
{
    [Fact]
    public void Nothing_shows_on_the_update_that_queued_it()
    {
        var srt = new StubSpeedrunTool();
        var popup = new QueuedPopup();

        popup.Queue("Session replaced");
        popup.Update(srt);

        Assert.Empty(srt.Popups);
    }

    [Fact]
    public void It_waits_while_a_speedrun_tool_popup_is_on_screen()
    {
        var srt = new StubSpeedrunTool();
        var popup = new QueuedPopup();

        popup.Queue("Session replaced");
        srt.PopupShowing = true;
        for (int i = 0; i < 90; i++) popup.Update(srt);
        Assert.Empty(srt.Popups);

        srt.PopupShowing = false;
        popup.Update(srt);
        Assert.Equal(["Session replaced"], srt.Popups);
    }

    [Fact]
    public void It_shows_once()
    {
        var srt = new StubSpeedrunTool();
        var popup = new QueuedPopup();

        popup.Queue("Session replaced");
        for (int i = 0; i < 5; i++) popup.Update(srt);

        Assert.Equal(["Session replaced"], srt.Popups);
    }

    [Fact]
    public void A_second_queue_replaces_the_first()
    {
        var srt = new StubSpeedrunTool();
        var popup = new QueuedPopup();

        popup.Queue("first");
        popup.Queue("second");
        popup.Update(srt);
        popup.Update(srt);

        Assert.Equal(["second"], srt.Popups);
    }

    [Fact]
    public void A_dropped_popup_never_shows()
    {
        var srt = new StubSpeedrunTool();
        var popup = new QueuedPopup();

        popup.Queue("Session replaced");
        popup.Drop();
        popup.Update(srt);
        popup.Update(srt);

        Assert.Empty(srt.Popups);
    }
}
