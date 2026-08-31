using System.Diagnostics;
using ImGuiNET;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

/// <summary>The sound notification settings block: what is loaded, and the buttons to manage it.</summary>
public static class SoundNotificationDisplay
{
    public static void Draw()
    {
        ImGui.TextUnformatted($"{SoundNotifier.SoundFileCount} wav files loaded");
        ImGui.TextUnformatted(
            "Put default.wav in the config directory for the fallback sound.\n" +
            "Name a file after a unique (Mageblood.wav) or after a rule file (unique.wav)\n" +
            "to give it its own alert. Most specific wins: unique, then rule, then default.");

        if (ImGui.Button("Reload sound list")) SoundNotifier.ReloadSoundList();

        ImGui.SameLine();
        if (ImGui.Button("Open sound folder"))
            Process.Start("explorer.exe", Main.ConfigDirectory);

        ImGui.SameLine();
        if (ImGui.Button("Reset alerted items")) SoundNotifier.Reset();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Let everything currently on the ground alert again. For testing.");
    }
}
