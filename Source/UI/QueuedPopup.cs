using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

/// <summary>
///     A popup shown once SpeedrunTool's own has left the level. SpeedrunTool draws every popup at
///     the same spot, and while it holds the game after a save it updates only the first tooltip in
///     the level: a popup shown from the save callback fades in alone, and SpeedrunTool's "Save to
///     [slot]" then fades in over it once the game resumes.
/// </summary>
internal sealed class QueuedPopup
{
    private string _message;
    // SpeedrunTool adds its popup after the save callback returns, and the level's entity list
    // takes it in at the start of the next update. Until then PopupShowing cannot see it.
    private bool _waitedOneUpdate;

    public void Queue(string message)
    {
        _message = message;
        _waitedOneUpdate = false;
    }

    public void Drop() => _message = null;

    // Once per update, frozen ones included: SpeedrunTool's popup runs out while it holds the game.
    public void Update(ISpeedrunTool srt)
    {
        if (_message == null) return;
        if (!_waitedOneUpdate)
        {
            _waitedOneUpdate = true;
            return;
        }
        if (srt.PopupShowing) return;
        string message = _message;
        _message = null;
        srt.Popup(message);
    }
}
