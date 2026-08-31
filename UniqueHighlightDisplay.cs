using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

/// <summary>
///     The "highlight these uniques" name list: its settings UI, and the match test the
///     overlay uses. Matching runs against the art-derived name candidates rather than the
///     filter engine, because an unidentified unique has no name for a filter to read - its
///     only identity is the art path, and that lives on CustomItemData, out of scope for a
///     compiled ItemFilter query.
/// </summary>
public static class UniqueHighlightDisplay
{
    public static bool Matches(CustomItemData item)
    {
        var settings = Main.Settings.UniqueHighlightSettings;
        if (!settings.Enable || item.UniqueNameCandidates.Count == 0) return false;

        return settings.Names.Any(entry => MatchesEntry(item.UniqueNameCandidates, entry, settings.ExactMatch));
    }

    private static bool MatchesEntry(List<string> candidates, string entry, bool exact)
    {
        if (string.IsNullOrWhiteSpace(entry)) return false;
        entry = entry.Trim();

        // Every candidate is checked, not just the first: art paths routinely map to several
        // uniques, and only testing candidates[0] would make the rest unmatchable forever.
        return candidates.Any(candidate => exact
            ? candidate.Equals(entry, StringComparison.OrdinalIgnoreCase)
            : candidate.Contains(entry, StringComparison.OrdinalIgnoreCase));
    }

    public static void Draw()
    {
        var settings = Main.Settings.UniqueHighlightSettings;
        var names = settings.Names;

        ImGui.TextUnformatted($"Unique names ({Main.UniqueArtMapping.Count} art paths loaded)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(
                "Enter the bare unique name, e.g. \"Timeclasp\" - not \"Timeclasp Moonstone Ring\".\n" +
                "If this reads 0 art paths the mapping failed to load and nothing will match.");
            ImGui.EndTooltip();
        }

        ImGui.Indent();

        for (var i = 0; i < names.Count; i++)
        {
            ImGui.PushID($"uniqueName_{i}");

            var name = names[i] ?? "";
            ImGui.SetNextItemWidth(220);
            if (ImGui.InputTextWithHint("", "Unique name...", ref name, 128)) names[i] = name;

            ImGui.SameLine();
            if (ImGui.Button("x"))
            {
                names.RemoveAt(i);
                ImGui.PopID();
                i--;
                continue;
            }

            DrawEntryStatus(name);
            ImGui.PopID();
        }

        if (ImGui.Button("+")) names.Add("");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a name");

        ImGui.Unindent();
    }

    /// <summary>
    ///     Tells the user, as they type, whether the name resolves and whether it shares art
    ///     with other uniques - so an unavoidable false positive is visible here rather than
    ///     being a surprise on the ground.
    /// </summary>
    private static void DrawEntryStatus(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var (known, artMates) = LookUp(name.Trim());

        ImGui.SameLine();
        if (!known)
        {
            ImGui.TextDisabled("(no art match - check the spelling)");
            return;
        }

        if (artMates.Count == 0)
        {
            ImGui.TextDisabled("(unambiguous)");
            return;
        }

        ImGui.TextDisabled($"(shares art with {string.Join(", ", artMates)})");
        if (!ImGui.IsItemHovered()) return;

        ImGui.BeginTooltip();
        ImGui.TextUnformatted(
            "These uniques use the same art, so an unidentified one of them\n" +
            "cannot be told apart until it is identified. They will highlight too.");
        ImGui.EndTooltip();
    }

    private static (bool Known, List<string> ArtMates) LookUp(string name)
    {
        var known = false;
        var artMates = new List<string>();

        foreach (var candidates in Main.UniqueArtMapping.Values)
        {
            // Mirror the Replica filtering CustomItemData applies, so the hint reflects
            // what will actually be matched at runtime rather than the raw mapping.
            var usable = candidates.Where(x => !x.StartsWith("Replica ")).ToList();
            if (!usable.Any(x => x.Contains(name, StringComparison.OrdinalIgnoreCase))) continue;

            known = true;
            artMates.AddRange(usable.Where(x => !x.Contains(name, StringComparison.OrdinalIgnoreCase)));
        }

        return (known, artMates.Distinct().OrderBy(x => x).ToList());
    }
}
