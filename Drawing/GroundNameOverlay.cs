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

        foreach (var item in items)
        {
            var (text, isWarning) = ResolveText(item, settings);
            if (text == null) continue;

            var box = item.Label?.GetClientRect() ?? RectangleF.Empty;
            if (box.Width <= 0 || box.Height <= 2) continue;
            if (tooltipRect.Intersects(box) || leftPanelRect.Intersects(box) || rightPanelRect.Intersects(box))
                continue;

            Color textColor, backgroundColor;
            if (isWarning)
            {
                textColor = Color.Red;
                backgroundColor = Color.Blue;
            }
            else if (item.EstimatedValue >= settings.ValuableValueThreshold.Value)
            {
                textColor = settings.ValuableNameTextColor;
                backgroundColor = settings.ValuableNameBackgroundColor;
            }
            else
            {
                textColor = settings.NameTextColor;
                backgroundColor = settings.NameBackgroundColor;
            }

            DrawOnItemLabel(drawList, box, BestFittingLayout(box, text), backgroundColor, textColor);
        }

        ImGui.End();
    }

    /// <summary>
    ///     Decides what to write on an item, in precedence order:
    ///     the matched rule's custom label, then resolved unique names, then the item name.
    ///     Returns null when the item should not be drawn at all.
    /// </summary>
    private static (string Text, bool IsWarning) ResolveText(CustomItemData item, GroundNameOverlaySettings settings)
    {
        var matched = item.IsWanted == true;

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

        return Math.Min(
            box.Width * Main.Settings.GroundNameOverlaySettings.LabelSize.Value / textSize.X,
            (box.Height - 2) / textSize.Y
        );
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
