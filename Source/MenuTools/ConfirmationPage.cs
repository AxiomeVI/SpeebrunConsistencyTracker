// MenuTools requires: TextMenuPage.cs IInputHoldingItem.cs
using System;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A page asking the player to confirm something, with a Yes and a No button. <see cref="OnConfirmed"/> runs if the
/// player picks Yes; <see cref="OnDeclined"/> runs if the page returns any other way (No, Cancel, ESC or Pause).
/// Neither runs if the scene ends under the page.
/// </summary>
public class ConfirmationPage : TextMenuPage {
    /// <summary>
    /// Invoked when the player picks Yes, after the page has returned and after <see cref="TextMenuPage.OnReturned"/>
    /// </summary>
    public Action OnConfirmed;

    /// <summary>
    /// Invoked when the page returns without the player picking Yes, after it has returned and after
    /// <see cref="TextMenuPage.OnReturned"/>
    /// </summary>
    public Action OnDeclined;

    /// <summary>The Yes button, e.g. to change its sound</summary>
    public Button YesButton { get; }

    /// <summary>The No button</summary>
    public Button NoButton { get; }

    private readonly bool defaultYes;
    private bool confirmed;

    /// <param name="parent">The menu to return to. Must not be null.</param>
    /// <param name="subMenuParent">The recursive submenu the page is entered from, if any</param>
    /// <param name="title">Header of the page; none if empty</param>
    /// <param name="message">Text shown under the header, split into lines on '\n'; none if empty</param>
    /// <param name="yesLabel">Label of the Yes button; vanilla's "Yes" in the player's language if null</param>
    /// <param name="noLabel">Label of the No button; vanilla's "No" in the player's language if null</param>
    /// <param name="defaultYes">Whether Yes is selected when the page is entered; No is by default</param>
    public ConfirmationPage(TextMenu parent, IInputHoldingItem subMenuParent = null, string title = "",
                            string message = "", string yesLabel = null, string noLabel = null,
                            bool defaultYes = false)
            : base(parent, subMenuParent) {
        this.defaultYes = defaultYes;
        if (!string.IsNullOrEmpty(title)) {
            Add(new Header(title));
        }
        if (!string.IsNullOrEmpty(message)) {
            foreach (string line in message.Replace("\r", "").Split('\n')) {
                // A sub-header without text has no height: a space keeps a blank line of the message
                Add(new SubHeader(line.Length > 0 ? line : " ", false));
            }
        }
        YesButton = new Button(yesLabel ?? Dialog.Clean("ASSIST_YES"));
        YesButton.Pressed(Confirm);
        NoButton = new Button(noLabel ?? Dialog.Clean("ASSIST_NO")) { ConfirmSfx = SFX.ui_main_button_back };
        NoButton.Pressed(Return);
        Add(YesButton);
        Add(NoButton);
    }

    /// <summary>
    /// Forgets the last answer and selects the default button. An override must call the base method, or a Yes
    /// from an earlier visit suppresses <see cref="OnDeclined"/>.
    /// </summary>
    protected override void SetUp() {
        confirmed = false;
        Selection = IndexOf(defaultYes ? YesButton : NoButton);
    }

    /// <summary>
    /// Returns as <see cref="TextMenuPage.Return"/> does, then invokes <see cref="OnDeclined"/> unless the player
    /// picked Yes. Does nothing if the page is not entered. An override must call the base method.
    /// </summary>
    public override void Return() {
        if (!Entered) {
            return;
        }
        // Read before returning: a handler of OnReturned may enter the page again, which forgets the answer
        bool declined = !confirmed;
        base.Return();
        if (declined) {
            OnDeclined?.Invoke();
        }
    }

    private void Confirm() {
        confirmed = true;
        Return();
        OnConfirmed?.Invoke();
    }
}
