using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;
using Warp.Model;

namespace Warp.UI;

/// <summary>How a button is dressed beyond its label: all off for the game's own look.</summary>
/// <param name="RestingOutline">
/// How visible the outline is when the pointer is not on the button, from 0 (hidden, as in the game's own buttons) to 1.
/// It is at full strength while the button is hot.
/// </param>
/// <param name="HoverGlow">Whether a halo in the button's color grows around it as the pointer comes.</param>
/// <param name="Icon">A mark drawn at the left of the label, in the label's color. None for no icon.</param>
internal sealed record ButtonStyle(
    float RestingOutline = 0f, bool HoverGlow = false, IReadOnlyList<IconPart> Icon = null);

/// <summary>
/// A rounded button with a text label in a color of its own. Clicking it sends its signal up the menu.
/// </summary>
internal sealed class WarpButton : ButtonTemplate
{
    private readonly RoundedRect _fill;
    private readonly RoundedRect _outline;
    private readonly ButtonStyle _style;
    private readonly RoundedRect[] _glow;
    private readonly FSprite[] _icon;

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

    /// <param name="style">How the button is dressed beyond its label. Null for the game's own look.</param>
    public WarpButton(
        Menu.Menu menu, MenuObject owner, string text, string signal, Vector2 pos, Vector2 size, Color tint,
        ButtonStyle style = null)
        : base(menu, owner, pos, size)
    {
        _style = style ?? new ButtonStyle();
        Tint = tint;
        DefaultTint = tint;
        Signal = signal;
        // Added first so the halo is drawn behind the button
        _glow = _style.HoverGlow ? [.. GlowLayers.Select(layer => AddGlow(layer.Grow))] : [];
        _fill = new RoundedRect(menu, this, Vector2.zero, size, true);
        subObjects.Add(_fill);
        _outline = new RoundedRect(menu, this, Vector2.zero, size, false);
        subObjects.Add(_outline);
        Label = new MenuLabel(menu, this, text, Vector2.zero, size, false);
        subObjects.Add(Label);
        _icon = [.. (_style.Icon ?? []).Select(_ => new FSprite("pixel"))];
        foreach (FSprite sprite in _icon)
        {
            Container.AddChild(sprite);
        }
        if (_icon.Length > 0)
        {
            // Room for the icon: the label sits a little right of the middle
            Label.pos = new Vector2(IconRoom, 0f);
        }
    }

    /// <summary>The halo's layers, from the outside in: how far each reaches past the button, and how strong it is.</summary>
    private static readonly (float Grow, float Strength)[] GlowLayers = [(8f, 0.1f), (5f, 0.16f), (2.5f, 0.24f)];

    /// <summary>How far the label moves right to leave room for an icon.</summary>
    private const float IconRoom = 7f;

    private RoundedRect AddGlow(float grow)
    {
        var halo = new RoundedRect(menu, this, new Vector2(-grow, -grow), size + new Vector2(2f * grow, 2f * grow), true);
        subObjects.Add(halo);
        return halo;
    }

    /// <summary> Takes the icon's pictures off the screen along with the button's own. </summary>
    public override void RemoveSprites()
    {
        base.RemoveSprites();
        foreach (FSprite sprite in _icon)
        {
            sprite.RemoveFromContainer();
        }
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
            sprite.alpha = Mathf.Max(pulse, _style.RestingOutline);
        }
        // Every sprite of a layer is set, because only its middle piece follows fillAlpha; the edges would stay solid
        float hover = Mathf.Lerp(buttonBehav.lastCol, buttonBehav.col, timeStacker);
        for (int i = 0; i < _glow.Length; i++)
        {
            foreach (FSprite sprite in _glow[i].sprites)
            {
                sprite.color = Tint;
                sprite.alpha = GlowLayers[i].Strength * hover;
            }
        }
        DrawIcon(timeStacker, color);
    }

    /// <summary> Places the icon's pieces on the button, at the left of the label, in the button's color. </summary>
    private void DrawIcon(float timeStacker, Color color)
    {
        IReadOnlyList<IconPart> parts = _style.Icon ?? [];
        Vector2 center = DrawPos(timeStacker) + new Vector2(IconRoom + 2f, size.y / 2f);
        for (int i = 0; i < _icon.Length; i++)
        {
            FSprite sprite = _icon[i];
            sprite.x = center.x + parts[i].X;
            sprite.y = center.y + parts[i].Y;
            sprite.scaleX = parts[i].Width;
            sprite.scaleY = parts[i].Height;
            sprite.rotation = parts[i].Rotation;
            sprite.color = color;
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
