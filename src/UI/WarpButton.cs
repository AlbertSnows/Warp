using Menu;
using UnityEngine;

namespace Warp.UI;

/// <summary>
/// A rounded button with a text label in a color of its own. Clicking it sends its signal up the menu.
/// </summary>
internal sealed class WarpButton : ButtonTemplate
{
    private readonly RoundedRect _fill;
    private readonly RoundedRect _outline;

    /// <summary>The color of the label and outline. Change it to show a state.</summary>
    public Color Tint
    {
        get; set;
    }

    /// <summary>The color the button was made with, which <see cref="Tint"/> goes back to after a highlight.</summary>
    public Color DefaultTint
    {
        get;
    }

    public MenuLabel Label
    {
        get;
    }

    /// <summary>The message sent up the menu when the button is clicked.</summary>
    public string Signal
    {
        get;
    }

    public WarpButton(
        Menu.Menu menu, MenuObject owner, string text, string signal, Vector2 pos, Vector2 size, Color tint)
        : base(menu, owner, pos, size)
    {
        Tint = tint;
        DefaultTint = tint;
        Signal = signal;
        _fill = new RoundedRect(menu, this, Vector2.zero, size, true);
        subObjects.Add(_fill);
        _outline = new RoundedRect(menu, this, Vector2.zero, size, false);
        subObjects.Add(_outline);
        Label = new MenuLabel(menu, this, text, Vector2.zero, size, false);
        subObjects.Add(Label);
    }

    public string Text
    {
        get => Label.text;
        set => Label.text = value;
    }

    public override void Update()
    {
        base.Update();
        buttonBehav.Update();
        var bump = buttonBehav.sizeBump + 0.5f * Mathf.Sin(buttonBehav.extraSizeBump * Mathf.PI);
        var pressed = buttonBehav.clicked ? 0f : 1f;
        _fill.fillAlpha = Mathf.Lerp(0.3f, 0.6f, buttonBehav.col);
        _fill.addSize = new Vector2(10f, 6f) * bump * pressed;
        _outline.addSize = new Vector2(2f, -2f) * bump * pressed;
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        Label.label.color = MyColor(timeStacker);
        var flash = Mathf.Lerp(buttonBehav.lastFlash, buttonBehav.flash, timeStacker);
        var fillColor = Color.Lerp(Menu.Menu.MenuRGB(Menu.Menu.MenuColors.Black), Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), flash);
        for (var i = 0; i < 9; i++)
        {
            _fill.sprites[i].color = fillColor;
        }
        var pulse = 0.5f + 0.5f * Mathf.Sin(Mathf.Lerp(buttonBehav.lastSin, buttonBehav.sin, timeStacker) / 30f * Mathf.PI * 2f);
        pulse *= buttonBehav.sizeBump;
        for (var i = 0; i < 8; i++)
        {
            _outline.sprites[i].color = MyColor(timeStacker);
            _outline.sprites[i].alpha = pulse;
        }
    }

    public override Color MyColor(float timeStacker)
    {
        var dark = new Color(0.05f, 0.05f, 0.05f);
        if (buttonBehav.greyedOut)
        {
            return Color.Lerp(Tint, dark, black);
        }
        var glow = Mathf.Max(
            Mathf.Lerp(buttonBehav.lastCol, buttonBehav.col, timeStacker),
            Mathf.Lerp(buttonBehav.lastFlash, buttonBehav.flash, timeStacker));
        var lit = Color.Lerp(Tint, Menu.Menu.MenuColor(Menu.Menu.MenuColors.White).rgb, glow);
        return Color.Lerp(lit, dark, black);
    }

    public override void Clicked() => Singal(this, Signal);
}
