using Menu;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The room and screen counts of the selected region, shown under the color key or beside the region buttons.
/// </summary>
internal sealed class StatsPanel : RectangularMenuObject
{
    private static readonly Color _textColor = new(0.65f, 0.65f, 0.65f);

    private readonly MenuLabel _summary;
    private readonly MenuLabel _breakdown;

    public StatsPanel(Menu.Menu menu, MenuObject owner) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _summary = AddLabel(new Vector2(22f, 0f));
        _breakdown = AddLabel(Vector2.zero);
    }

    /// <summary> Adds an empty left-aligned label that hangs from its position. </summary>
    private MenuLabel AddLabel(Vector2 labelPos)
    {
        var label = new MenuLabel(menu, this, "", labelPos, Vector2.zero, false)
        {
            label =
            {
                color = _textColor, alignment = FLabelAlignment.Left, anchorY = 1f
            }
        };
        subObjects.Add(label);
        return label;
    }

    /// <summary> Shows the counts for a region. </summary>
    public void Show(RegionStats stats)
    {
        (string summary, string breakdown) = stats.Describe(Translator.Translate);
        _summary.label.text = summary;
        _breakdown.label.text = breakdown;
        _breakdown.pos.x = _summary.pos.x;
        FollowSummary();
    }

    /// <summary> Puts the panel and its text where the layout says. </summary>
    public void Place(StatsPlacement placement)
    {
        this.MoveTo(new Vector2(placement.PanelX, placement.PanelY));
        _summary.SetY(placement.TextY);
        FollowSummary();
    }

    /// <summary> Keeps the screen breakdown under the summary as the summary changes height. </summary>
    public override void Update()
    {
        base.Update();
        FollowSummary();
    }

    /// <summary>The screen breakdown hangs under the summary, wherever its text ends.</summary>
    private void FollowSummary() => _breakdown.SetY(_summary.pos.y - _summary.label.textRect.height);
}
