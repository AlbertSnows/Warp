using System.Collections.Generic;
using Menu;
using UnityEngine;

namespace Warp.UI;

/// <summary> Things the menu parts do to their children again and again. </summary>
internal static class MenuObjectExtensions
{
    /// <summary>
    /// Takes the items off the screen and out of the owner. Items that are null are skipped, so a part that may not
    /// have been made yet can be listed. The sprites go first and then the object, as upstream Warp does it.
    /// </summary>
    public static void RemoveAll(this MenuObject owner, IEnumerable<MenuObject> items)
    {
        foreach (MenuObject item in items)
        {
            if (item == null)
            {
                continue;
            }

            item.RemoveSprites();
            owner.RemoveSubObject(item);
        }
    }

    /// <summary> Moves a control to a place at once, without it sliding there over the next frames. </summary>
    public static void MoveTo(this RectangularMenuObject item, Vector2 pos)
    {
        item.pos = pos;
        item.lastPos = pos;
    }

    /// <summary> Moves a control to a height at once, keeping its place across. </summary>
    public static void SetY(this RectangularMenuObject item, float y) => item.MoveTo(new Vector2(item.pos.x, y));

    /// <summary> Moves a control to a place across at once, keeping its height. </summary>
    public static void SetX(this RectangularMenuObject item, float x) => item.MoveTo(new Vector2(x, item.pos.y));
}

/// <summary> Sizes and places the menu shares. </summary>
internal static class UiLayout
{
    /// <summary> A height far enough below the screen that nothing there can be seen, used to hide a part of the menu. </summary>
    public const float OffScreenY = -2000f;
}
