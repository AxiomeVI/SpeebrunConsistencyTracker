// MenuTools requires: nothing else
using System;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A <see cref="TextMenu.Item"/> that handles input itself while it has the selection, as recursive submenus do, so
/// unfocusing the <see cref="TextMenu"/> it is in doesn't stop it. Pages and the room chooser take input away from
/// such an item while they are open through this interface, which lets them be used without the submenu classes.
/// </summary>
public interface IInputHoldingItem {
    /// <summary>
    /// The innermost item that handles input, this one or one nested in it, or null if none does
    /// </summary>
    IInputHoldingItem InputHolder { get; }

    /// <summary>
    /// Stop the item from handling input until the returned action is called
    /// </summary>
    /// <param name="stopAutoScroll">Also stop it from scrolling its menu in the meantime</param>
    /// <returns>The action that gives the item back the input state it had</returns>
    Action SuspendInput(bool stopAutoScroll);
}
