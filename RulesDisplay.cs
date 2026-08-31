using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using ExileCore;
using ImGuiNET;
using ItemFilterLibrary;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

public class RulesDisplay
{
    public static void DrawSettings()
    {

        ImGui.Separator();
        if (ImGui.Button("Clear StoredCustomItems and ReRun (PROFILER)"))
        {
            Main.StoredCustomItems.Clear();
            Main.UpdateStoredItems(true, true);
        }

        if (ImGui.Button("Recheck all StoredCustomItems for IsWanted (PROFILER)"))
        {
            foreach (var item in Main.StoredCustomItems)
            {
                item.IsWanted = null;
                item.MatchedRule = null;
                item.WasDynamicallyUpdated = false;
            }

            Main.UpdateStoredItems(true, true);
        }
        ImGui.Separator();
        if (ImGui.Button("Open Filter Folder"))
        {
            var configDirectory = Main.ConfigDirectory;
            var customConfigDirectory = !string.IsNullOrEmpty(Main.Settings.CustomConfigDir)
                ? Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory)!, Main.Settings.CustomConfigDir)
                : null;

            var directoryToOpen = Directory.Exists(customConfigDirectory)
                ? customConfigDirectory
                : configDirectory;

            Process.Start("explorer.exe", directoryToOpen);
        }

        if (ImGui.Button("Reload Rules"))
            LoadAndApplyRules();

        ImGui.Separator();
        ImGui.Text(
            "Rule Files\nFiles are loaded in order, so easier to process (common item queries hit more often that others) rule sets should be loaded first.");
        ImGui.Separator();

        if (ImGui.BeginTable("RulesTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Drag", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Toggle", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.None);
            ImGui.TableSetupColumn("Ground Label", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("Colors", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Frame", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Sound", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();

            var rules = Main.Settings.GroundRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.PushID($"drag_{rule.Location}");

                var dropTargetStart = ImGui.GetCursorScreenPos();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.Button("=", new Vector2(30, 20));
                ImGui.PopStyleColor();

                if (ImGui.BeginDragDropSource())
                {
                    ImGuiHelpers.SetDragDropPayload("RuleIndex", i);
                    ImGui.TextUnformatted(rule.Name);
                    ImGui.EndDragDropSource();
                }
                else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Drag me to reorder");
                }

                ImGui.SetCursorScreenPos(dropTargetStart);
                ImGui.InvisibleButton($"dropTarget_{rule.Location}", new Vector2(30, 20));

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGuiHelpers.AcceptDragDropPayload<int>("RuleIndex");
                    if (payload != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        var movedRule = rules[payload.Value];
                        rules.RemoveAt(payload.Value);
                        rules.Insert(i, movedRule);
                        LoadAndApplyRules();
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                ImGui.PushID($"toggle_{rule.Location}");
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("", ref enabled))
                {
                    rule.Enabled = enabled;
                    LoadAndApplyRules();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(2);
                ImGui.PushID(rule.Location);

                var directoryPart =
                    Path.GetDirectoryName(rule.Location)?.Replace("\\", "/") ?? "";
                var fileName = Path.GetFileName(rule.Location);
                var fileFullPath = Path.Combine(GetPickitConfigFileDirectory(), rule.Location);

                var cellWidth = ImGui.GetContentRegionAvail().X;

                ImGui.InvisibleButton($"FileCell_{rule.Location}", new Vector2(cellWidth, ImGui.GetFrameHeight()));

                ImGui.SameLine();

                StartContextMenu(fileName, fileFullPath, $"FileCell_{rule.Location}");

                var textPos = ImGui.GetItemRectMin();
                ImGui.SetCursorScreenPos(textPos);

                // File names are user data and may contain '%', so never let them reach
                // the printf-style Text/TextColored overloads.
                if (!string.IsNullOrEmpty(directoryPart))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.7f, 1.0f, 1.0f));
                    ImGui.TextUnformatted(directoryPart + "/");
                    ImGui.PopStyleColor();
                    ImGui.SameLine(0, 0);
                }

                ImGui.TextUnformatted(fileName);

                ImGui.PopID();

                ImGui.TableSetColumnIndex(3);
                ImGui.PushID($"label_{rule.Location}");
                var customLabel = rule.CustomLabel ?? "";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("", ref customLabel, 64)) rule.CustomLabel = customLabel;

                // SetTooltip/Text are printf-style natively, so anything containing '%'
                // must go through TextUnformatted or ImGui will read bogus varargs and crash.
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(
                        "Text drawn on the ground label of items this rule matches.\n" +
                        "Leave empty to fall back to the item name.\n" +
                        "%N = item name, %U = resolved unique names, %V = estimated value, \\n = new line");
                    ImGui.EndTooltip();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(4);
                ImGui.PushID($"colors_{rule.Location}");
                var useCustomColors = rule.UseCustomColors;
                if (ImGui.Checkbox("", ref useCustomColors)) rule.UseCustomColors = useCustomColors;

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Draw matches of this rule in its own colours.\nText swatch first, then background.");
                    ImGui.EndTooltip();
                }

                if (useCustomColors)
                {
                    const ImGuiColorEditFlags swatchFlags =
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaPreview;

                    var ruleTextColor = rule.TextColor;
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("text", ref ruleTextColor, swatchFlags)) rule.TextColor = ruleTextColor;

                    var ruleBackgroundColor = rule.BackgroundColor;
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("background", ref ruleBackgroundColor, swatchFlags))
                        rule.BackgroundColor = ruleBackgroundColor;
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(5);
                ImGui.PushID($"frame_{rule.Location}");
                var drawFrame = rule.DrawFrame;
                if (ImGui.Checkbox("", ref drawFrame)) rule.DrawFrame = drawFrame;

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(
                        "Draw a frame around the ground label of this rule's matches.\n" +
                        "Thickness is shared, under Ground Name Overlay settings.");
                    ImGui.EndTooltip();
                }

                if (drawFrame)
                {
                    var ruleFrameColor = rule.FrameColor;
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("frame", ref ruleFrameColor,
                            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel |
                            ImGuiColorEditFlags.AlphaPreview))
                        rule.FrameColor = ruleFrameColor;
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(6);
                ImGui.PushID($"sound_{rule.Location}");
                var playSound = rule.PlaySound;
                if (ImGui.Checkbox("", ref playSound)) rule.PlaySound = playSound;

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(
                        "Play a sound the first time a match of this rule appears.\n" +
                        $"Uses {Path.GetFileNameWithoutExtension(rule.Location)}.wav if present, else default.wav.\n" +
                        "Needs Sound Notification Settings enabled.");
                    ImGui.EndTooltip();
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        void StartContextMenu(string fileName, string fileFullPath, string contextMenuId)
        {
            if (ImGui.BeginPopupContextItem(contextMenuId))
            {
                if (ImGui.MenuItem("Open"))
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileFullPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugWindow.LogError(
                            $"[DrawSettings] Failed to open file: {ex.Message}",
                            10
                        );
                    }

                ImGui.EndPopup();
            }
        }
    }
    private static string GetPickitConfigFileDirectory()
    {
        var pickitConfigFileDirectory = Main.ConfigDirectory;
        if (!string.IsNullOrEmpty(Main.Settings.CustomConfigDir))
        {
            var customConfigFileDirectory = Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory),
                Main.Settings.CustomConfigDir);
            if (Directory.Exists(customConfigFileDirectory))
                pickitConfigFileDirectory = customConfigFileDirectory;
            else
                DebugWindow.LogError("[Ground Items] Custom config folder does not exist.", 10);
        }

        return pickitConfigFileDirectory;
    }

    private static ItemFilter LoadItemFilterWithRetry(string rulePath)
    {
        const int maxRetries = 10;
        var attempt = 0;
        while (true)
            try
            {
                return ItemFilter.LoadFromPath(rulePath);
            }
            catch (IOException ex)
            {
                attempt++;
                if (attempt >= maxRetries)
                    throw new IOException($"Failed to load file after {maxRetries} attempts: {rulePath}", ex);
                Thread.Sleep(100);
            }
    }

    public static void LoadAndApplyRules()
    {
        var pickitConfigFileDirectory = GetPickitConfigFileDirectory();
        var existingRules = Main.Settings.GroundRules;
        try
        {
            var diskFiles = new DirectoryInfo(pickitConfigFileDirectory)
                .GetFiles("*.ifl", SearchOption.AllDirectories)
                .ToList();

            var newRules = diskFiles
                .Select(fileInfo => new GroundRule(
                    fileInfo.Name,
                    Path.GetRelativePath(pickitConfigFileDirectory, fileInfo.FullName),
                    false))
                .ExceptBy(existingRules.Select(rule => rule.Location), groundRule => groundRule.Location)
                .ToList();

            foreach (var groundRule in existingRules)
            {
                var fullPath = Path.Combine(pickitConfigFileDirectory, groundRule.Location);
                if (File.Exists(fullPath))
                    newRules.Add(groundRule);
                else
                    DebugWindow.LogError($"[LoadAndApplyRules] File '{groundRule.Name}' not found.", 10);
            }

            Main.ItemFilters = newRules
                .Where(rule => rule.Enabled)
                .Select(rule =>
                {
                    var rulePath = Path.Combine(pickitConfigFileDirectory, rule.Location);
                    return new LoadedRule(rule, LoadItemFilterWithRetry(rulePath));
                })
                .ToList();

            Main.Settings.GroundRules = newRules;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"[LoadAndApplyRules] An error occurred while loading rule files: {e.Message}", 10);
        }

        Main.StoredCustomItems.Clear();
        Main.UpdateStoredItems(true);
    }
}
