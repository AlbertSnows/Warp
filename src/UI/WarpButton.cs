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

    private MenuLabel Label
    {
        get;
    }

    /// <summary>The message sent up the menu when the button is clicked.</summary>
    private string Signal
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

    /// <summary> Animates the button's size and fill as the pointer comes and goes. </summary>
    public override void Update()
    {
        base.Update();
        buttonBehav.Update();
        float bump = buttonBehav.sizeBump + 0.5f * Mathf.Sin(buttonBehav.extraSizeBump * Mathf.PI);
        float pressed = buttonBehav.clicked ? 0f : 1f;
        _fill.fillAlpha = Mathf.Lerp(0.3f, 0.6f, buttonBehav.col);
        _fill.addSize = new Vector2(10f, 6f) * bump * pressed;
        _outline.addSize = new Vector2(2f, -2f) * bump * pressed;
    }

    /// <summary> Draws the label, fill and outline in the button's colors. </summary>
    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        Color color = MyColor(timeStacker);
        Label.label.color = color;
        var fillColor = Color.Lerp(Menu.Menu.MenuRGB(Menu.Menu.MenuColors.Black), Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White),
            buttonBehav.Flash(timeStacker));
        foreach (FSprite sprite in _fill.sprites)
        {
            sprite.color = fillColor;
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Mathf.Lerp(buttonBehav.lastSin, buttonBehav.sin, timeStacker) / 30f * Mathf.PI * 2f);
        pulse *= buttonBehav.sizeBump;
        foreach (FSprite sprite in _outline.sprites)
        {
            sprite.color = color;
            sprite.alpha = pulse;
        }
    }

    /// <summary>
    /// The button's color now: its tint, lightened while the pointer is on it and darkened when greyed out.
    /// </summary>
    public override Color MyColor(float timeStacker)
    {
        var dark = new Color(0.05f, 0.05f, 0.05f);
        if (buttonBehav.greyedOut)
        {
            return Color.Lerp(Tint, dark, black);
        }
        var lit = Color.Lerp(Tint, Menu.Menu.MenuColor(Menu.Menu.MenuColors.White).rgb, buttonBehav.Glow(timeStacker));
        return Color.Lerp(lit, dark, black);
    }

    /// <summary> Sends the button's signal up the menu. </summary>
    public override void Clicked() => Singal(this, Signal);
}
