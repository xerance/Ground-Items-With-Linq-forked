using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using Ground_Items_With_Linq.Drawing;
using ItemFilterLibrary;
using Newtonsoft.Json;

namespace Ground_Items_With_Linq;

public class GroundItemsWithLinq : BaseSettingsPlugin<GroundItemsWithLinqSettings>
{
    public const string CustomUniqueArtMappingPath = "uniqueArtMapping.json";
    public const string DefaultUniqueArtMappingPath = "uniqueArtMapping.default.json";
    public static GroundItemsWithLinq Main;
    public readonly HashSet<CustomItemData> StoredCustomItems = [];
    public readonly Stopwatch Timer = Stopwatch.StartNew();

    public List<LoadedRule> ItemFilters;
    public Element LargeMap;
    public Dictionary<string, List<string>> UniqueArtMapping = [];

    public GroundItemsWithLinq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        Main = this;
        GameController.UnderPanel.WantUse(() => Settings.Enable);

        Settings.UniqueIdentificationSettings.RebuildUniqueItemArtMappingBackup.OnPressed += () =>
        {
            var mapping = UniqueArtManager.GetGameFileUniqueArtMapping();

            if (mapping != null)
                File.WriteAllText(
                    Path.Join(DirectoryFullName, CustomUniqueArtMappingPath),
                    JsonConvert.SerializeObject(mapping, Formatting.Indented)
                );
        };

        Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping.OnValueChanged += (_, _) =>
        {
            UniqueArtMapping = UniqueArtManager.LoadUniqueArtMapping(
                Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping
            );
        };

        // Load up front rather than waiting for the first AreaChange: the settings UI reads
        // this mapping, and without it every highlight name reports "no art match" until the
        // player happens to zone. Falls back to the embedded JSON when game files are not
        // ready yet, so it is safe this early.
        EnsureUniqueArtMapping();
        SoundNotifier.ReloadSoundList();

        RulesDisplay.LoadAndApplyRules();
        return true;
    }

    /// <summary>Populates <see cref="UniqueArtMapping" /> if it is empty. Safe to call at any time.</summary>
    public void EnsureUniqueArtMapping(bool force = false)
    {
        if (UniqueArtMapping.Count != 0 && !force) return;

        try
        {
            UniqueArtMapping = UniqueArtManager.LoadUniqueArtMapping(
                Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping
            );
        }
        catch (System.Exception ex)
        {
            LogError($"Unable to load the unique art mapping: {ex.Message}");
        }
    }

    public override void OnLoad()
    {
        Graphics.InitImage("directions.png");
    }

    public override void AreaChange(AreaInstance area)
    {
        UniqueArtMapping = UniqueArtManager.LoadUniqueArtMapping(
            Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping
        );
        StoredCustomItems.Clear();

        // Leaving and returning should alert again; standing next to an item should not.
        SoundNotifier.Reset();
    }

    public override Job Tick()
    {
        LargeMap = GameController.IngameState.IngameUi.Map.LargeMap;
        UpdateStoredItems(false);

        // Driven from Tick, not Render, so alerts still fire while a panel is covering the screen.
        SoundNotifier.Process(StoredCustomItems);

        return null;
    }

    public override void Render()
    {
        var inGameUi = GameController.Game.IngameState.IngameUi;

        if (!Settings.IgnoreFullscreenPanels && inGameUi.FullscreenPanels.Any(x => x.IsVisible)) return;
        if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible) return;

        if (Settings.SortMode is null)
        {
            Settings.SortMode = new()
            {
                Values = [SortModes.None, SortModes.Distance, SortModes.EstimatedValueDescending],
                Value = SortModes.Distance
            };
        }

        if (Settings.SortMode.Values is null || Settings.SortMode.Values.Count == 0)
        {
            Settings.SortMode.Values = [SortModes.None, SortModes.Distance, SortModes.EstimatedValueDescending];
        }

        if (string.IsNullOrEmpty(Settings.SortMode.Value))
        {
            Settings.SortMode.Value = SortModes.Distance;
        }

        var wantedItems = Settings.SortMode.Value switch
        {
            SortModes.Distance => StoredCustomItems.Where(item => item.IsWanted == true).OrderBy(item => item.DistanceCustom).ToList(),
            SortModes.EstimatedValueDescending => StoredCustomItems.Where(item => item.IsWanted == true).OrderByDescending(item => item.EstimatedValue).ToList(),
            _ => StoredCustomItems.Where(item => item.IsWanted == true).ToList()
        };

        GroundNameOverlay.Render(StoredCustomItems);

        if (wantedItems.Count <= 0) return;

        DrawingLabels.RenderItemsOnScreen(wantedItems);
    }

    public void UpdateStoredItems(bool forceUpdate)
    {
        UpdateStoredItems(forceUpdate, false);
    }

    public void UpdateStoredItems(bool forceUpdate, bool doProfiler)
    {
        if (Timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

        var profilerTotal = doProfiler ? Stopwatch.StartNew() : null;
        var profilerModifyStored = doProfiler ? Stopwatch.StartNew() : null;

        ItemStateManager.RefreshStoredItems(Settings.UseFastLabelList);

        profilerModifyStored?.Stop();
        var profilerModifyLoopStored = doProfiler ? Stopwatch.StartNew() : null;
        var profilerIsInFilter = doProfiler ? Stopwatch.StartNew() : null;

        foreach (var item in StoredCustomItems)
        {
            if (item.WasDynamicallyUpdated)
            {
                item.IsWanted = null;
                item.MatchedRule = null;
                item.WasDynamicallyUpdated = false;
            }

            item.UpdateDynamicCustomData();

            profilerIsInFilter?.Start();
            if (item.IsWanted == null)
            {
                var match = ItemFilters?.FirstOrDefault(x => x.Filter.Matches(item));
                item.MatchedRule = match?.Rule;
                item.IsWanted = match != null;
            }
            profilerIsInFilter?.Stop();
        }

        if (doProfiler)
            Profiler.LogPerformanceMetrics(profilerModifyLoopStored, profilerTotal, profilerModifyStored,
                profilerIsInFilter);

        Timer.Restart();
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        RulesDisplay.DrawSettings();
    }
}

/// <summary>A rule paired with the filter it loaded, so a match can be traced back to its settings.</summary>
public record LoadedRule(GroundRule Rule, ItemFilter Filter);
