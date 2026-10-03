// MenuTools requires: IInputHoldingItem.cs
using System;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A full-screen menu entered from another menu (possibly another page), which it hides until the page returns
/// </summary>
public class TextMenuPage : TextMenu {
    protected TextMenu parent;
    protected IInputHoldingItem subMenuParent;

    /// <summary>
    /// Invoked when the page returns to its parent, however that happens
    /// </summary>
    public Action OnReturn;

    /// <summary>
    /// Whether the page is currently shown, between <see cref="Enter"/> and <see cref="Return"/>
    /// </summary>
    protected bool Entered { get; private set; }

    // Gives the input back to the submenu the selection was in when the page was entered: subMenuParent, or the one
    // found in the parent
    private Action restoreSubMenuInput;
    private bool parentWasFocused;
    private bool parentWasAutoscrolling;

    /// <param name="parent">The menu to return to</param>
    /// <param name="subMenuParent">
    ///     The recursive submenu the page is entered from, if any. If null, the submenu that has the selection in
    ///     <paramref name="parent"/> when the page is entered is used.
    /// </param>
    public TextMenuPage(TextMenu parent, IInputHoldingItem subMenuParent = null) {
        this.parent                = parent;
        this.subMenuParent         = subMenuParent;
        OnCancel = OnESC = delegate {
            if (Input.MenuCancel.Pressed || Input.ESC.Pressed) {
                // If I had control of everything, I wouldn't want to play the SFX here. This is how vanilla
                // menus do it though, so this should be consistent with that so my workaround works for both.
                Audio.Play(SFX.ui_main_button_back);
            }
            Return();
        };
        OnPause = ReturnToRootAndPause;
    }

    public virtual void Enter() {
        if (Entered) {
            // Entering twice would save the already-unfocused parent state and never restore it
            return;
        }
        Entered                = true;
        parentWasFocused       = parent.Focused;
        parentWasAutoscrolling = parent.AutoScroll;
        parent.Focused         = false;
        parent.AutoScroll      = false;
        restoreSubMenuInput    = (subMenuParent ?? (parent.Current as IInputHoldingItem)?.InputHolder)
                                     ?.SuspendInput(stopAutoScroll: true);
        parent.Visible = false;
        Active         = true;
        Visible        = true;
        if (Scene?.Entities.removing.Contains(this) ?? false) {
            // Entered again in the frame it returned: the page is still in the scene, waiting to be removed, and Add
            // would do nothing. Take it off the removal list instead.
            Scene.Entities.removing.Remove(this);
            Scene.Entities.toRemove.Remove(this);
        } else {
            Engine.Scene.Add(this);
        }
        Current?.SelectWiggler.Start();
        Current?.OnEnter?.Invoke();
    }

    public virtual void Return() {
        if (!Entered) {
            return;
        }
        Entered = false;
        Current?.OnLeave?.Invoke();
        parent.Focused    = parentWasFocused;
        parent.AutoScroll = parentWasAutoscrolling;
        restoreSubMenuInput?.Invoke();
        restoreSubMenuInput = null;
        parent.Visible = true;
        RemoveSelf();
        // This is awful but easy
        Input.MenuConfirm.ConsumePress();
        Input.MenuCancel.ConsumePress();
        Input.Pause.ConsumePress();
        Input.ESC.ConsumePress();
        OnReturn?.Invoke();
    }

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
    public TextMenu ReturnToRoot() {
        TextMenu menu = this;
        while (menu is TextMenuPage page) {
            menu = page.parent;
            page.Return();
        }
        return menu;
    }

    // Pause closes every page and then acts as if it was pressed on the menu underneath, like vanilla's options
    // menu does in game (where the pause menu then unpauses)
    private void ReturnToRootAndPause() {
        TextMenu root = ReturnToRoot();
        if (root.OnPause != null) {
            // A recursive submenu holding the selection leaves the menu unfocused, and Everest's in-game Mod Options
            // only pauses while focused. Restore the old focus if the handler didn't close the menu.
            bool rootWasFocused = root.Focused;
            root.Focused = true;
            root.OnPause();
            if (root.Focused) {
                root.Focused = rootWasFocused;
            }
        } else {
            Audio.Play(SFX.ui_main_button_back);
        }
    }

    public override void Update() {
        base.Update();
        if (Focused && !Input.MenuConfirm.Pressed) {
            if (Input.Pause.Pressed) {
                ReturnToRootAndPause();
            } else if (Input.MenuCancel.Pressed || Input.ESC.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Return();
            }
        }
    }
}
