// MenuTools requires: IInputHoldingItem.cs
using System;
using System.Runtime.ExceptionServices;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A full-screen menu entered from another menu (possibly another page), which it hides until the page returns.
/// Add items as on any <see cref="TextMenu"/>, then call <see cref="Enter"/>. Cancel, ESC and Pause return one page:
/// no button closes every page, and a level stays paused. While the page is entered, the recursive submenus of its
/// parent handle no button (<see cref="MenuInputBlock"/>), even if a handler moves the selection into one.
/// </summary>
/// <remarks>
/// The constructor sets <see cref="TextMenu.OnCancel"/>, <see cref="TextMenu.OnESC"/> and
/// <see cref="TextMenu.OnPause"/>, and <see cref="Update"/> handles the same three buttons itself. A handler assigned
/// to one of the three therefore runs in addition to the page's own action unless it consumes the press. To react
/// to the page closing, use <see cref="OnReturned"/>. An override of <see cref="Enter"/>, <see cref="Return"/>,
/// <c>Added</c>, <c>Removed</c> or <c>SceneEnd</c> must call the base method. So must one of <see cref="Update"/>,
/// unless the page handles all its input itself, as the text entry pages do.
/// </remarks>
public class TextMenuPage : TextMenu {
    /// <summary>The menu the page hides while it is entered, and returns to</summary>
    protected TextMenu parent;
    /// <summary>
    /// The recursive submenu given to the constructor, or null to use the one that has the selection in
    /// <see cref="parent"/> when the page is entered
    /// </summary>
    protected IInputHoldingItem subMenuParent;

    /// <summary>
    /// Invoked when the page has returned to its parent, however that happened. A page with an outcome of its own
    /// (accepted, canceled, confirmed, declined) invokes that callback right after this one. So every callback of a
    /// page runs with the parent shown again and with the focus it had when the page was entered: the menu's own, or
    /// the input of the submenu the page was entered from. A handler can enter another page.
    /// </summary>
    public Action OnReturned;

    /// <summary>Whether the page is currently shown, between <see cref="Enter"/> and <see cref="Return"/></summary>
    protected bool Entered { get; private set; }

    // Gives the input back to the submenu the selection was in when the page was entered: subMenuParent, or the one
    // found in the parent
    private Action restoreSubMenuInput;
    // Lets the parent's recursive submenus handle input again
    private Action releaseParentInput;
    private bool parentWasFocused;
    private bool parentWasAutoscrolling;
    // Whether SetUp has run with no CleanUp since
    private bool needsCleanUp;
    // The overworld whose Confirm and Back hints the page hid, and whether it was showing them
    private Overworld hintsOverworld;
    private bool overworldHintsShown;

    /// <param name="parent">The menu to return to. Must not be null.</param>
    /// <param name="subMenuParent">
    ///     The recursive submenu the page is entered from, if any. If null (the default), the submenu that has the
    ///     selection in <paramref name="parent"/> when the page is entered is used.
    /// </param>
    public TextMenuPage(TextMenu parent, IInputHoldingItem subMenuParent = null) {
        this.parent                = parent;
        this.subMenuParent         = subMenuParent;
        OnCancel = OnESC = OnPause = delegate {
            if (Input.MenuCancel.Pressed || Input.ESC.Pressed || Input.Pause.Pressed) {
                // The handler plays the back sound, as vanilla menus' handlers do: a recursive submenu that passes
                // a Cancel on to its menu counts on it and plays none itself
                Audio.Play(SFX.ui_main_button_back);
            }
            Return();
        };
    }

    /// <summary>
    /// Shows the page over its parent. The parent is hidden and unfocused, the recursive submenu the page was entered
    /// from stops handling input, and the page joins the current scene with its selected item hovered.
    /// <see cref="SetUp"/> runs first. Does nothing if the page is already entered.
    /// </summary>
    public virtual void Enter() {
        if (Entered) {
            // Entering twice would save the already-unfocused parent state and never restore it
            return;
        }
        SetUp();
        // A key bound to Confirm and to Pause leaves Pause buffered: the press that opened the page must not close it
        Input.Pause.ConsumeBuffer();
        needsCleanUp           = true;
        Entered                = true;
        parentWasFocused       = parent.Focused;
        parentWasAutoscrolling = parent.AutoScroll;
        parent.Focused         = false;
        parent.AutoScroll      = false;
        // The innermost submenu that handles the input: a caller may have given one that holds a nested one
        restoreSubMenuInput    = (subMenuParent?.InputHolder ?? subMenuParent ??
                                      (parent.Current as IInputHoldingItem)?.InputHolder)
                                     ?.SuspendInput(stopAutoScroll: true);
        // Also when no submenu had the input: a handler can move the selection into one while the page is open
        releaseParentInput     = MenuInputBlock.Block(parent);
        parent.Visible = false;
        Active         = true;
        Visible        = true;
        if (Scene?.Entities.removing.Contains(this) ?? false) {
            // Entered again in the frame it returned: the page is still in the scene, waiting to be removed, and Add
            // would do nothing. Take it off the removal list instead.
            Scene.Entities.removing.Remove(this);
            Scene.Entities.toRemove.Remove(this);
        } else {
            Scene scene = Engine.Scene;
            scene.Add(this);
            // The scene takes the page in at the start of its next frame. If it ends before that, it never tells
            // the page, and what SetUp took would stay taken.
            scene.OnEndOfFrame += () => {
                if (Scene == null && Engine.NextScene != scene) {
                    RunCleanUp();
                }
            };
        }
        Current?.SelectWiggler.Start();
        Current?.OnEnter?.Invoke();
    }

    /// <summary>
    /// Goes back to the parent. <see cref="CleanUp"/> runs, the page leaves the scene, and the parent gets back its
    /// visibility, focus, scrolling and the submenu's input. Then <see cref="OnReturned"/> is invoked. Consumes
    /// Confirm, Cancel, Pause and ESC first, so the press that closed the page does not act on the parent in the same
    /// frame. Does nothing if the page is not entered. If <see cref="CleanUp"/> or the <c>OnLeave</c> of the selected
    /// item throws, all of this still happens, then the exception leaves this method: a callback its caller invokes
    /// after it (accepted, canceled, declined) does not run.
    /// </summary>
    public virtual void Return() {
        if (!Entered) {
            return;
        }
        Entered = false;
        try {
            RunCleanUp();
        } finally {
            // A CleanUp that throws must not leave the parent hidden and unfocused: the page has returned
            FinishReturn();
        }
    }

    private void FinishReturn() {
        try {
            Current?.OnLeave?.Invoke();
        } finally {
            // An OnLeave that throws must not leave the parent hidden and unfocused either
            GiveParentBack();
        }
    }

    private void GiveParentBack() {
        parent.Focused    = parentWasFocused;
        parent.AutoScroll = parentWasAutoscrolling;
        releaseParentInput?.Invoke();
        releaseParentInput = null;
        restoreSubMenuInput?.Invoke();
        restoreSubMenuInput = null;
        if (MenuInputBlock.HolderIn(parent) != null) {
            // A handler moved the selection into a submenu while the page was open: it has the input, not the menu
            parent.Focused = false;
        }
        parent.Visible = true;
        // The scene only removes the page at the start of the next frame, and would draw it once more over its parent
        Visible = false;
        RemoveSelf();
        // The press that closed the page must not act on the parent in the same frame
        Input.MenuConfirm.ConsumePress();
        Input.MenuCancel.ConsumePress();
        Input.Pause.ConsumePress();
        Input.ESC.ConsumePress();
        OnReturned?.Invoke();
    }

    /// <summary>
    /// Called by <see cref="Enter"/> before the page is shown, to prepare what it needs while it is open. An override
    /// in a subclass of a page that has one must call the base method.
    /// </summary>
    protected virtual void SetUp() {}

    /// <summary>
    /// Called once for every <see cref="SetUp"/>, as soon as the page stops being shown: by <see cref="Return"/>,
    /// before any handler runs, or else when the page is removed from the scene or the scene ends. Let go here of
    /// whatever <see cref="SetUp"/> took. An override in a subclass of a page that has one must call the base method.
    /// It is called once even if it throws: the page still returns, and the exception reaches the caller of
    /// <see cref="Return"/>.
    /// </summary>
    protected virtual void CleanUp() {}

    /// <summary>
    /// Hides the overworld's Confirm and Back hints, for a page that draws its own. Call it from
    /// <see cref="SetUp"/>, and <see cref="RestoreOverworldHints"/> from <see cref="CleanUp"/>.
    /// </summary>
    protected void HideOverworldHints() {
        hintsOverworld = Engine.Scene as Overworld;
        if (hintsOverworld != null) {
            overworldHintsShown        = hintsOverworld.ShowInputUI;
            hintsOverworld.ShowInputUI = false;
        }
    }

    /// <summary>Gives the overworld back the hints that <see cref="HideOverworldHints"/> hid</summary>
    protected void RestoreOverworldHints() {
        if (hintsOverworld != null) {
            hintsOverworld.ShowInputUI = overworldHintsShown;
            hintsOverworld             = null;
        }
    }

    private void RunCleanUp() {
        if (needsCleanUp) {
            needsCleanUp = false;
            CleanUp();
        }
    }

    /// <summary>
    /// Returns if the page left the scene while entered, as after <c>Close()</c> or <c>RemoveSelf()</c>:
    /// <see cref="CleanUp"/> runs, the parent gets back what <see cref="Enter"/> took, and <see cref="OnReturned"/> is
    /// invoked. An override must call the base method.
    /// </summary>
    public override void Removed(Scene scene) {
        base.Removed(scene);
        bool wasEntered = Entered;
        Entered = false;
        try {
            RunCleanUp();
        } finally {
            if (wasEntered) {
                // Not Return(): TextMenu.Close() has already called the selected item's OnLeave
                GiveParentBack();
            }
        }
    }

    /// <summary>
    /// Runs <see cref="CleanUp"/> if it is still owed, when the scene ends under the page (a page entered in the
    /// scene's last frame gets the same from <see cref="Enter"/>). The page does not return:
    /// its parent stays hidden, unfocused and blocked (<see cref="MenuInputBlock"/>), and must not be shown again in
    /// another scene. An override must call the base method, or its <see cref="CleanUp"/> does not run on this route.
    /// </summary>
    public override void SceneEnd(Scene scene) {
        base.SceneEnd(scene);
        RunCleanUp();
    }

    /// <summary>
    /// Takes the page out again if it returned in the frame it was entered, before the scene had added it
    /// </summary>
    public override void Added(Scene scene) {
        base.Added(scene);
        // A page returned in the same frame it was entered was still waiting to be added to the scene, so Return's
        // RemoveSelf did nothing. Take it out now, and keep it from updating or rendering in the meantime.
        if (!Entered) {
            Active  = false;
            Visible = false;
            RemoveSelf();
        }
    }

    /// <summary>
    /// Return from this page and every page it was entered from, back to the first menu that isn't a page
    /// </summary>
    /// <returns>That menu</returns>
    /// <remarks>
    /// If a page's <see cref="CleanUp"/> throws, every page is still closed, then the first exception is thrown
    /// </remarks>
    public TextMenu ReturnToRoot() {
        TextMenu menu = this;
        ExceptionDispatchInfo failure = null;
        while (menu is TextMenuPage page) {
            menu = page.parent;
            try {
                page.Return();
            } catch (Exception e) {
                // A page whose CleanUp threw has returned all the same: close the rest, then report it
                failure ??= ExceptionDispatchInfo.Capture(e);
            }
        }
        failure?.Throw();
        return menu;
    }

    /// <summary>Updates the menu, then returns on Cancel, ESC or Pause if the press is still there</summary>
    public override void Update() {
        base.Update();
        // Not redundant with OnCancel, OnESC and OnPause. The page can be unfocused when TextMenu.Update dispatches
        // them and focused again later in the frame with the press unconsumed: an auto-exit submenu on its last row
        // that gets Down and Cancel in one frame gives the focus back and consumes only Down.
        if (Focused && !Input.MenuConfirm.Pressed &&
                (Input.MenuCancel.Pressed || Input.ESC.Pressed || Input.Pause.Pressed)) {
            Audio.Play(SFX.ui_main_button_back);
            Return();
        }
    }
}
