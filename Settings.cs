using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
using SharpDX;
using System.Collections.Generic;
using GameOffsets.Native;

namespace Ground_Items_With_Linq;

public class GroundItemsWithLinqSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);

    [Menu(null, "Display debug strings")]
    public ToggleNode Debug { get; set; } = new(false);

    public List<GroundRule> GroundRules { get; set; } = [];
    public RangeNode<int> UpdateTimer { get; set; } = new(500, 0, 5000);
    public RangeNode<float> TextSize { get; set; } = new(1f, 1f, 20f);

    public UniqueIdentificationSettings UniqueIdentificationSettings { get; set; } = new();
    public GroundNameOverlaySettings GroundNameOverlaySettings { get; set; } = new();
    public UniqueHighlightSettings UniqueHighlightSettings { get; set; } = new();
    public SoundNotificationSettings SoundNotificationSettings { get; set; } = new();
    public ToggleNode EnableTextDrawing { get; set; } = new(true);
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new(false);
    public ToggleNode IgnoreRightPanels { get; set; } = new(false);
    public TextNode FontOverride { get; set; } = new("");
    public ToggleNode ScaleFontWhenCustom { get; set; } = new(false);
    public RangeNode<int> ItemSpacing { get; set; } = new(5, 1, 60);
    public ToggleNode AlignItemTextToCenter { get; set; } = new(true);
    public ToggleNode DrawCompass { get; set; } = new(true);
    public ToggleNode AlignCompassToCenter { get; set; } = new(true);

    [Menu(null, "Use a much more performant label list, only containing labels which are actually visible (items hidden by the filter or by pressing Z will not show up)")]
    public ToggleNode UseFastLabelList { get; set; } = new(false);

    [JsonProperty("textPadding2")]
    public RangeNode<Vector2i> TextPadding { get; set; } = new(new Vector2i(5, 2), Vector2i.Zero, Vector2i.One * 60);

    public RangeNode<int> BorderWidth { get; set; } = new(1, 1, 20);
    public RangeNode<int> LabelShift { get; set; } = new(0, -600, 600);
    public ListNode SortMode { get; set; } = new()
    {
        Values = new List<string>
        {
            SortModes.None,
            SortModes.Distance,
            SortModes.EstimatedValueDescending
        },
        Value = SortModes.Distance
    };

    public ToggleNode EnableMapDrawing { get; set; } = new(true);
    public ColorNode MapLineColor { get; set; } = new(new Color(214, 0, 255, 255));
    public RangeNode<float> MapLineThickness { get; set; } = new(2.317f, 1f, 10f);

    public SocketDisplaySettings SocketDisplaySettings { get; set; } = new();
    public EstimatedValueDisplaySettings EstimatedValueDisplaySettings { get; set; } = new();

    [Menu(@"Use a Custom '\config\custom_folder' folder")]
    public TextNode CustomConfigDir { get; set; } = new();
}

[Submenu]
public class SocketDisplaySettings
{
    public ToggleNode ShowSockets { get; set; } = new(true);
    public RangeNode<int> SocketSize { get; set; } = new(6, 1, 60);
    public RangeNode<int> SocketSpacing { get; set; } = new(4, 4, 60);
    public RangeNode<int> SocketPadding { get; set; } = new(5, 0, 60);
    public ColorNode RedSocketColor { get; set; } = new Color(201, 13, 50, 255);
    public ColorNode GreenSocketColor { get; set; } = new Color(158, 202, 13, 255);
    public ColorNode BlueSocketColor { get; set; } = new Color(88, 130, 254, 255);
    public ColorNode WhiteSocketColor { get; set; } = Color.White;
    public ColorNode AbyssalSocketColor { get; set; } = new Color(59, 59, 59, 255);
    public ColorNode ResonatorSocketColor { get; set; } = new Color(249, 149, 13, 255);
    public ColorNode LinkColor { get; set; } = new Color(195, 195, 195, 255);
    public RangeNode<int> LinkWidth { get; set; } = new(4, 2, 20);
}

[Submenu]
public class EstimatedValueDisplaySettings
{
    public ToggleNode EnableEstimatedValueDisplay { get; set; } = new(true);
    public RangeNode<float> MinimumValueToDisplay { get; set; } = new(1f, 0f, 1000f);
    public RangeNode<int> MaxDecimals { get; set; } = new(0, 0, 8);
    public TextNode ValueText { get; set; } = new(" [%Vc]");
}

[Submenu]
public class UniqueIdentificationSettings
{
    [JsonIgnore]
    public ButtonNode RebuildUniqueItemArtMappingBackup { get; set; } = new();

    [Menu(null, "Use if you want to ignore what's in game memory and rely only on your custom/builtin file")]
    public ToggleNode IgnoreGameUniqueArtMapping { get; set; } = new(false);
}

[Submenu]
public class GroundNameOverlaySettings
{
    [Menu(null, "Draw a name on top of the item's label on the ground")]
    public ToggleNode Enable { get; set; } = new(true);

    [Menu(null, "Draw for every item that matches one of your filters, using the rule's custom label if it has one")]
    public ToggleNode DrawForAllFilterMatches { get; set; } = new(true);

    [Menu(null, "Label EVERY unidentified unique, not just the highlighted ones. Noisy: the " +
                "highlight list and your filters already cover what you care about")]
    public ToggleNode DrawForAllUnidentifiedUniques { get; set; } = new(false);

    [Menu(null, "Draw ??? over unidentified uniques whose art path is not in the mapping")]
    public ToggleNode ShowWarningTextForUnknownUniques { get; set; } = new(false);

    [Menu(null, "Skip uniques that resolve to a single name (the art already gives it away)")]
    public ToggleNode HideSingleCandidateNames { get; set; } = new(false);

    [Menu(null, "Estimated value at or above which an item is drawn with the valuable colors")]
    public RangeNode<float> ValuableValueThreshold { get; set; } = new(10f, 0f, 1000f);

    [Menu(null, "Fraction of the label width the text is allowed to fill")]
    public RangeNode<float> LabelSize { get; set; } = new(0.8f, 0.1f, 1f);

    [Menu(null, "Multiplies the fitted text size. 1 = fit the label exactly, higher spills past its edges")]
    public RangeNode<float> TextScale { get; set; } = new(1f, 0.1f, 5f);

    [Menu(null, "Stretch the background across the whole ground label instead of hugging the text")]
    public ToggleNode StretchBackgroundToLabel { get; set; } = new(false);

    [Menu(null, "Thickness of the frame drawn around a rule's matches")]
    public RangeNode<int> RuleFrameThickness { get; set; } = new(2, 1, 10);

    public ColorNode NameTextColor { get; set; } = new(Color.Black);
    public ColorNode NameBackgroundColor { get; set; } = new(new Color(175, 96, 37));
    public ColorNode ValuableNameTextColor { get; set; } = new(new Color(175, 96, 37));
    public ColorNode ValuableNameBackgroundColor { get; set; } = new(Color.White);
}

[Submenu]
public class UniqueHighlightSettings
{
    public UniqueHighlightSettings()
    {
        // Rendered inside this submenu rather than appended to the bottom of the
        // settings window, so the name rows sit with the options that govern them.
        NameList = new CustomNode { DrawDelegate = UniqueHighlightDisplay.Draw };
    }

    [Menu(null, "Highlight named uniques on the ground even when no filter matches them")]
    public ToggleNode Enable { get; set; } = new(true);

    [Menu(null, "Require the whole name to match. Off, an entry matches any name containing it")]
    public ToggleNode ExactMatch { get; set; } = new(false);

    [Menu(null, "Also draw a frame around the item's ground label")]
    public ToggleNode DrawLabelFrame { get; set; } = new(true);

    public RangeNode<int> FrameThickness { get; set; } = new(2, 1, 10);
    public ColorNode FrameColor { get; set; } = new(Color.Wheat);
    public ColorNode TextColor { get; set; } = new(Color.Black);
    public ColorNode BackgroundColor { get; set; } = new(new Color(255, 215, 0));

    /// <summary>
    ///     Bare unique names, e.g. "Timeclasp" - never the base type. Matched against the
    ///     art-derived candidates, which is the only identity an unidentified unique has.
    /// </summary>
    public List<string> Names { get; set; } = [];

    [JsonIgnore]
    public CustomNode NameList { get; set; }
}

[Submenu]
public class SoundNotificationSettings
{
    public SoundNotificationSettings()
    {
        Information = new CustomNode { DrawDelegate = SoundNotificationDisplay.Draw };
    }

    [Menu(null, "Play a sound the first time a wanted item appears on the ground")]
    public ToggleNode Enable { get; set; } = new(false);

    [Menu(null, "Play for uniques matched by the highlight name list")]
    public ToggleNode PlayForHighlightedUniques { get; set; } = new(true);

    public RangeNode<float> Volume { get; set; } = new(1f, 0f, 2f);

    [JsonIgnore]
    public CustomNode Information { get; set; }
}

public class GroundRule(string name, string location, bool enabled)
{
    public string Name { get; set; } = name;
    public string Location { get; set; } = location;
    public bool Enabled { get; set; } = enabled;

    /// <summary>
    ///     Optional text drawn on the ground label of items this rule matches.
    ///     Supports %N (item name), %U (resolved unique names) and %V (estimated value).
    /// </summary>
    public string CustomLabel { get; set; } = "";

    /// <summary>Draw matches of this rule in its own colours instead of the global ones.</summary>
    public bool UseCustomColors { get; set; }

    /// <summary>
    ///     RGBA in the 0-1 range. Stored as a Vector4 so it round-trips through the settings
    ///     JSON without a converter and feeds ImGui's colour picker directly.
    /// </summary>
    public System.Numerics.Vector4 TextColor { get; set; } = new(0f, 0f, 0f, 1f);

    public System.Numerics.Vector4 BackgroundColor { get; set; } = new(0.686f, 0.376f, 0.145f, 1f);

    /// <summary>Play a sound the first time an item this rule matches appears on the ground.</summary>
    public bool PlaySound { get; set; }

    /// <summary>Draw a frame around the ground label of items this rule matches.</summary>
    public bool DrawFrame { get; set; }

    public System.Numerics.Vector4 FrameColor { get; set; } = new(0.96f, 0.87f, 0.70f, 1f);
}

public static class SortModes
{
    public const string None = "None";
    public const string Distance = "Distance";
    public const string EstimatedValueDescending = "Estimated Value";
}
