using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq.Drawing;

/// <summary>
///     Draws a name on top of an item's label on the ground.
///     The fit-to-label technique comes from Get-Chaos-Value's ShowRealUniqueNameOnGround, but
///     this draws through ExileCore's Graphics rather than ImGui. ImGui window state is global to
///     the host, so an exception raised between Begin and End leaves the stack unbalanced and
///     breaks every ImGui overlay drawn after it that frame, in this plugin or any other.
/// </summary>
public static class GroundNameOverlay
{
    public static void Render(IEnumerable<CustomItemData> items)
    {
        var settings = Main.Settings.GroundNameOverlaySettings;
        if (!settings.Enable) return;

        var ingameUi = Main.GameController.IngameState.IngameUi;

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
            var text = ResolveText(item, settings, isHighlighted);
            if (string.IsNullOrEmpty(text)) continue;

            var box = item.Label?.GetClientRect() ?? RectangleF.Empty;
            if (box.Width <= 0 || box.Height <= 2) continue;
            if (tooltipRect.Intersects(box) || leftPanelRect.Intersects(box) || rightPanelRect.Intersects(box))
                continue;

            var (textColor, backgroundColor) = ResolveColors(item, settings, isHighlighted);

            DrawOnItemLabel(box, BestFittingLayout(box, text), backgroundColor, textColor);

            // The highlight frame wins over a rule frame, matching the colour precedence.
            if (isHighlighted && highlight.DrawLabelFrame)
                DrawFrame(box, highlight.FrameThickness.Value, highlight.FrameColor);
            else if (item.IsWanted == true && item.MatchedRule is { DrawFrame: true } framedRule)
                DrawFrame(box, settings.RuleFrameThickness.Value, ToColor(framedRule.FrameColor));
        }
    }

    /// <summary>
    ///     Picks the colours for an item, in precedence order: the highlight list,
    ///     the matched rule's own colours, the valuable colours, the defaults.
    /// </summary>
    private static (Color Text, Color Background) ResolveColors(CustomItemData item,
        GroundNameOverlaySettings settings, bool isHighlighted)
    {
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
    ///     Decides what to write on an item. Only two things earn a label: a name you put on the
    ///     highlight list, and an item one of your filters asked for. Anything else draws nothing,
    ///     which is the whole point - a label on every unique would bury both.
    ///     Returns null when the item should not be drawn.
    /// </summary>
    private static string ResolveText(CustomItemData item, GroundNameOverlaySettings settings, bool isHighlighted)
    {
        // A highlighted unique draws whether or not a filter wanted it - the point of the
        // list is to catch things your filters do not cover.
        if (isHighlighted) return JoinCandidates(item);

        if (item.IsWanted != true || !settings.DrawForAllFilterMatches) return null;

        var template = item.MatchedRule?.CustomLabel;
        return !string.IsNullOrWhiteSpace(template) ? ApplyTemplate(template, item) : item.Name;
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

    private static void DrawFrame(RectangleF box, int thickness, Color color)
    {
        // Inflate by half the thickness so the stroke sits outside the label rather than
        // eating into the item art it is meant to be framing.
        var frame = box;
        frame.Inflate(thickness / 2f, thickness / 2f);
        Main.Graphics.DrawFrame(frame, color, thickness);
    }

    private static void DrawOnItemLabel(RectangleF box, (string Text, float Scale) layout,
        Color backgroundColor, Color textColor)
    {
        using (Main.Graphics.SetTextScale(layout.Scale))
        {
            var textSize = Main.Graphics.MeasureText(layout.Text);
            var textPosition = new Vector2N(
                box.Center.X - textSize.X / 2,
                box.Center.Y - textSize.Y / 2);

            // Hugging the text keeps the item art visible either side; stretching covers the
            // whole label, which reads as a solid block of colour in a busy loot pile.
            var stretch = Main.Settings.GroundNameOverlaySettings.StretchBackgroundToLabel;
            var left = stretch ? box.Left : textPosition.X;
            var right = stretch ? box.Right : textPosition.X + textSize.X;

            Main.Graphics.DrawBox(
                new RectangleF(left, box.Top + 1, right - left, box.Height - 2),
                backgroundColor);
            Main.Graphics.DrawText(layout.Text, textPosition, textColor);
        }
    }
}
