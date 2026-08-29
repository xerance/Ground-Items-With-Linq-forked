using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq.Drawing;

/// <summary>
///     Draws the resolved unique name on top of the item's label on the ground.
///     Ported from Get-Chaos-Value's ShowRealUniqueNameOnGround, but gated on the
///     Ground Items With Linq filter result instead of on a price threshold.
/// </summary>
public static class GroundNameOverlay
{
    public static void Render(IEnumerable<CustomItemData> items)
    {
        var settings = Main.Settings.UniqueIdentificationSettings;
        if (!settings.ShowRealUniqueNameOnGround) return;

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
            if (settings.OnlyShowForFilterMatches && item.IsWanted != true) continue;

            var isValuable = item.EstimatedValue >= settings.ValuableValueThreshold.Value;
            if (settings.OnlyShowRealUniqueNameForValuableUniques && !isValuable) continue;

            var box = item.Label?.GetClientRect() ?? RectangleF.Empty;
            if (box.Width <= 0 || box.Height <= 2) continue;
            if (tooltipRect.Intersects(box) || leftPanelRect.Intersects(box) || rightPanelRect.Intersects(box))
                continue;

            if (item.UniqueNameCandidates.Count != 0)
            {
                if (settings.HideSingleCandidateNames && item.UniqueNameCandidates.Count == 1) continue;

                Color textColor = isValuable
                    ? settings.ValuableUniqueItemNameTextColor
                    : settings.UniqueItemNameTextColor;
                Color backgroundColor = isValuable
                    ? settings.ValuableUniqueItemNameBackgroundColor
                    : settings.UniqueItemNameBackgroundColor;

                // Try every "names per line" layout and keep whichever fits the label box best.
                var (text, ratio) = Enumerable.Range(1, item.UniqueNameCandidates.Count)
                    .Select(perOneLine => string.Join('\n', item.UniqueNameCandidates
                        .Chunk(perOneLine)
                        .Select(onLine => string.Join(" / ", onLine))))
                    .Select(candidate => (text: candidate, ratio: GetRatio(box, candidate)))
                    .MaxBy(x => x.ratio);

                DrawOnItemLabel(drawList, box, ratio, text, backgroundColor, textColor);
            }
            else if (settings.ShowWarningTextForUnknownUniques && item.IsUnidentifiedUnique)
            {
                const string text = "???";
                DrawOnItemLabel(drawList, box, GetRatio(box, text), text, Color.Blue, Color.Red);
            }
        }

        ImGui.End();
    }

    private static float GetRatio(RectangleF box, string text)
    {
        var textSize = Main.Graphics.MeasureText(text);
        if (textSize.X <= 0 || textSize.Y <= 0) return 0;

        return Math.Min(
            box.Width * Main.Settings.UniqueIdentificationSettings.UniqueLabelSize.Value / textSize.X,
            (box.Height - 2) / textSize.Y
        );
    }

    private static void DrawOnItemLabel(ImDrawListPtr drawList, RectangleF box, float scale, string text,
        Color backgroundColor, Color textColor)
    {
        ImGui.SetWindowFontScale(scale);
        var textSize = ImGui.CalcTextSize(text);
        var textPosition = box.Center.ToVector2Num() - textSize / 2;
        var rectPosition = new Vector2N(textPosition.X, box.Top + 1);
        drawList.AddRectFilled(rectPosition, rectPosition + new Vector2N(textSize.X, box.Height - 2),
            backgroundColor.ToImgui());
        drawList.AddText(textPosition, textColor.ToImgui(), text);
        ImGui.SetWindowFontScale(1);
    }
}
