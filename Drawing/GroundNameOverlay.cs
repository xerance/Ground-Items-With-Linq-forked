using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq.Drawing;

/// <summary>
///     Draws a name on top of an item's label on the ground.
///     The drawing technique is ported from Get-Chaos-Value's ShowRealUniqueNameOnGround,
///     but the text is not limited to uniques: any item matching a filter can be labelled,
///     and a rule can supply its own template via <see cref="GroundRule.CustomLabel" />.
/// </summary>
public static class GroundNameOverlay
{
    public static void Render(IEnumerable<CustomItemData> items)
    {
        var settings = Main.Settings.GroundNameOverlaySettings;
        if (!settings.Enable) return;

        var ingameUi = Main.GameController.IngameState.IngameUi;

        // ImGui font scaling only works inside a window, so we open a throwaway one
        // and draw into the background list. Same trick Get-Chaos-Value uses.
        ImGui.Begin("GroundItemsWithLinq_NameOverlay",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);

        var drawList = ImGui.GetBackgroundDrawList();

        var tooltipRect = ingameUi.ItemOnGroundTooltip is { Address: not 0, IsVisible: true } tooltip
            ? tooltip.GetClientRectCache
            : RectangleF.Empty;
        var leftPanelRect = ingameUi.OpenLeftPanel.Address != 0
            ? ingameUi.OpenLeftPanel.GetClientRectCache
            : RectangleF.Empty;
        var rightPanelRect = ingameUi.OpenRightPanel.Address != 0
            ? ingameUi.OpenRightPanel.GetClientRectCache
            : RectangleF.Empty;

        var highlight = Main.Settings.UniqueHighlightSettings;

        foreach (var item in items)
        {
            var isHighlighted = UniqueHighlightDisplay.Matches(item);
            var (text, isWarning) = ResolveText(item, settings, isHighlighted);
            if (text == null) continue;

            var box = item.Label?.GetClientRect() ?? RectangleF.Empty;
            if (box.Width <= 0 || box.Height <= 2) continue;
            if (tooltipRect.Intersects(box) || leftPanelRect.Intersects(box) || rightPanelRect.Intersects(box))
                continue;

            var (textColor, backgroundColor) = ResolveColors(item, settings, isWarning, isHighlighted);

            DrawOnItemLabel(drawList, box, BestFittingLayout(box, text), backgroundColor, textColor);

            if (isHighlighted && highlight.DrawLabelFrame)
            {
                var thickness = highlight.FrameThickness.Value;
                drawList.AddRect(
                    new Vector2N(box.Left - thickness / 2f, box.Top - thickness / 2f),
                    new Vector2N(box.Right + thickness / 2f, box.Bottom + thickness / 2f),
                    highlight.FrameColor.Value.ToImgui(), 0f, ImDrawFlags.None, thickness);
            }
        }

        ImGui.End();
    }

    /// <summary>
    ///     Picks the colours for an item, in precedence order: the unknown-unique warning,
    ///     the highlight list, the matched rule's own colours, the valuable colours, the defaults.
    /// </summary>
    private static (Color Text, Color Background) ResolveColors(CustomItemData item,
        GroundNameOverlaySettings settings, bool isWarning, bool isHighlighted)
    {
        if (isWarning) return (Color.Red, Color.Blue);

        // A name you typed by hand is the most specific statement of intent there is,
        // so it outranks both the matched rule's colours and the valuable colours.
        if (isHighlighted)
        {
            var highlight = Main.Settings.UniqueHighlightSettings;
            return (highlight.TextColor, highlight.BackgroundColor);
        }

        var rule = item.MatchedRule;
        if (item.IsWanted == true && rule is { UseCustomColors: true })
            return (ToColor(rule.TextColor), ToColor(rule.BackgroundColor));

        if (item.EstimatedValue >= settings.ValuableValueThreshold.Value)
            return (settings.ValuableNameTextColor, settings.ValuableNameBackgroundColor);

        return (settings.NameTextColor, settings.NameBackgroundColor);
    }

    private static Color ToColor(System.Numerics.Vector4 v)
    {
        return new Color(v.X, v.Y, v.Z, v.W);
    }

    /// <summary>
    ///     Decides what to write on an item, in precedence order:
    ///     the matched rule's custom label, then resolved unique names, then the item name.
    ///     Returns null when the item should not be drawn at all.
    /// </summary>
    private static (string Text, bool IsWarning) ResolveText(CustomItemData item, GroundNameOverlaySettings settings,
        bool isHighlighted)
    {
        var matched = item.IsWanted == true;

        // A highlighted unique draws whether or not a filter wanted it - the whole point
        // of the list is to catch things your filters do not cover.
        if (isHighlighted) return (JoinCandidates(item), false);

        if (matched && settings.DrawForAllFilterMatches)
        {
            var template = item.MatchedRule?.CustomLabel;
            if (!string.IsNullOrWhiteSpace(template)) return (ApplyTemplate(template, item), false);
        }

        if (item.IsUnidentifiedUnique && settings.DrawForUnidentifiedUniques)
        {
            if (item.UniqueNameCandidates.Count != 0)
            {
                if (settings.HideSingleCandidateNames && item.UniqueNameCandidates.Count == 1) return (null, false);
                return (JoinCandidates(item), false);
            }

            if (settings.ShowWarningTextForUnknownUniques) return ("???", true);
        }

        // No custom label and nothing unique-specific to say: fall back to the item's own name.
        if (matched && settings.DrawForAllFilterMatches) return (item.Name, false);

        return (null, false);
    }

    private static string ApplyTemplate(string template, CustomItemData item)
    {
        return template
            .Replace("%N", item.Name ?? "")
            .Replace("%U", JoinCandidates(item))
            .Replace("%V", item.EstimatedValue.ToString("#,0.##", CultureInfo.InvariantCulture))
            .Replace("\\n", "\n");
    }

    private static string JoinCandidates(CustomItemData item)
    {
        return item.UniqueNameCandidates.Count != 0 ? string.Join(" / ", item.UniqueNameCandidates) : "";
    }

    /// <summary>
    ///     Tries every "entries per line" split of the text and keeps whichever fills the label box best.
    /// </summary>
    private static (string Text, float Scale) BestFittingLayout(RectangleF box, string text)
    {
        var parts = text.Split(" / ", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return (text, GetRatio(box, text));

        return Enumerable.Range(1, parts.Length)
            .Select(perLine => string.Join('\n', parts.Chunk(perLine).Select(line => string.Join(" / ", line))))
            .Select(candidate => (Text: candidate, Scale: GetRatio(box, candidate)))
            .MaxBy(x => x.Scale);
    }

    private static float GetRatio(RectangleF box, string text)
    {
        var textSize = Main.Graphics.MeasureText(text);
        if (textSize.X <= 0 || textSize.Y <= 0) return 0;

        var settings = Main.Settings.GroundNameOverlaySettings;

        // Fit the text to the label box, then bias it by the user's scale. Without the
        // fit the text would overflow small labels; without the bias it could never be
        // made deliberately smaller or allowed to spill past the label edges.
        var fitted = Math.Min(
            box.Width * settings.LabelSize.Value / textSize.X,
            (box.Height - 2) / textSize.Y
        );

        return fitted * settings.TextScale.Value;
    }

    private static void DrawOnItemLabel(ImDrawListPtr drawList, RectangleF box, (string Text, float Scale) layout,
        Color backgroundColor, Color textColor)
    {
        ImGui.SetWindowFontScale(layout.Scale);
        var textSize = ImGui.CalcTextSize(layout.Text);
        var textPosition = box.Center.ToVector2Num() - textSize / 2;
        var rectPosition = new Vector2N(textPosition.X, box.Top + 1);
        drawList.AddRectFilled(rectPosition, rectPosition + new Vector2N(textSize.X, box.Height - 2),
            backgroundColor.ToImgui());
        drawList.AddText(textPosition, textColor.ToImgui(), layout.Text);
        ImGui.SetWindowFontScale(1);
    }
}
