using Menu;
using UnityEngine;

namespace Warp.UI;

/// <summary>A button with a picture on it, drawn in a color of its own.</summary>
internal sealed class WarpSymbolButton : SymbolButton
{
    /// <summary> The color of the symbol and its outline. </summary>
    public Color Tint { get; set; } = new(0.7f, 0.7f, 0.7f);

    public WarpSymbolButton(Menu.Menu menu, MenuObject owner, string symbolName, string signal, Vector2 pos, float size)
        : base(menu, owner, symbolName, signal, pos)
    {
        roundedRect.size = new Vector2(size, size);
        this.size = roundedRect.size;
    }

    /// <summary> The symbol's color now: its tint, brighter while the pointer is on it. </summary>
    public override Color MyColor(float timeStacker)
    {
        if (!buttonBehav.greyedOut)
        {
            var from = Color.Lerp(Color.Lerp(Menu.Menu.MenuColor(Menu.Menu.MenuColors.DarkGrey).rgb, Tint, 0.7f), Tint,
                buttonBehav.Glow(timeStacker));
            return Color.Lerp(from, Menu.Menu.MenuColor(Menu.Menu.MenuColors.Black).rgb, black);
        }
        if (maintainOutlineColorWhenGreyedOut)
        {
            return Menu.Menu.MenuRGB(Menu.Menu.MenuColors.DarkGrey);
        }
        return HSLColor.Lerp(
            Menu.Menu.MenuColor(Menu.Menu.MenuColors.VeryDarkGrey), Menu.Menu.MenuColor(Menu.Menu.MenuColors.Black), black).rgb;
    }

    /// <summary> Colors the symbol. </summary>
    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        symbolSprite.color = MyColor(timeStacker);
    }
}
