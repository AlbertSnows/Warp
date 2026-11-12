using Menu;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The room and screen counts of the selected region, shown under the colour key or beside the region buttons.
/// </summary>
internal sealed class StatsPanel : RectangularMenuObject
{
    private static readonly Color TextColor = new(0.65f, 0.65f, 0.65f);

    private readonly MenuLabel _summary;
    private readonly MenuLabel _breakdown;

    public StatsPanel(Menu.Menu menu, MenuObject owner) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _summary = AddLabel(new Vector2(22f, 0f));
        _breakdown = AddLabel(Vector2.zero);
    }

    private MenuLabel AddLabel(Vector2 pos)
    {
        var label = new MenuLabel(menu, this, "", pos, Vector2.zero, false);
        label.label.color = TextColor;
        label.label.alignment = FLabelAlignment.Left;
        label.label.anchorY = 1f;
        subObjects.Add(label);
        return label;
    }

    public void Show(RegionStats stats)
    {
        var (summary, breakdown) = stats.Describe(Translator.Translate);
        _summary.label.text = summary;
        _breakdown.label.text = breakdown;
        _breakdown.pos.x = _summary.pos.x;
        FollowSummary();
    }

    public void Place(StatsPlacement placement)
    {
        pos = new Vector2(placement.PanelX, placement.PanelY);
        lastPos = pos;
        _summary.pos.y = placement.TextY;
        _summary.lastPos.y = placement.TextY;
        FollowSummary();
    }

    public override void Update()
    {
        base.Update();
        FollowSummary();
    }

    /// <summary>The screen breakdown hangs under the summary, wherever its text ends.</summary>
    private void FollowSummary()
    {
        _breakdown.pos.y = _summary.pos.y - _summary.label.textRect.height;
        _breakdown.lastPos.y = _breakdown.pos.y;
    }
}
