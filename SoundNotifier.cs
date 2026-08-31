using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExileCore;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

/// <summary>
///     Plays a one-shot sound when a wanted item appears on the ground.
///     Ported from Get-Chaos-Value's sound notifications, but triggered by the two things this
///     plugin actually knows - a rule matched, or the highlight list matched - rather than by a
///     poe.ninja price threshold, which there is no feed for here.
/// </summary>
public static class SoundNotifier
{
    private const string DefaultSoundFile = "default";

    private static Dictionary<string, string> _soundFiles =
        new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    ///     Ground labels already alerted, so an item lying in view beeps once rather than every
    ///     tick. Keyed by label address, which is what StoredCustomItems is keyed on too.
    /// </summary>
    private static readonly HashSet<long> Alerted = [];

    public static int SoundFileCount => _soundFiles.Count;

    public static void Reset()
    {
        Alerted.Clear();
    }

    public static void ReloadSoundList()
    {
        ExtractDefaultSound();

        try
        {
            _soundFiles = Directory.EnumerateFiles(Main.ConfigDirectory, "*.wav")
                .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Path: path))
                .DistinctBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(x => x.Name, x => x.Path, StringComparer.InvariantCultureIgnoreCase);
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"[SoundNotifier] Unable to read sound files: {ex.Message}");
            _soundFiles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }
    }

    /// <summary>
    ///     Writes the bundled default.wav into the config directory on first run, so the feature
    ///     works out of the box. Never overwrites: the file is the user's once it exists, and
    ///     replacing their chosen alert on every reload would be obnoxious.
    /// </summary>
    private static void ExtractDefaultSound()
    {
        try
        {
            var target = Path.Join(Main.ConfigDirectory, $"{DefaultSoundFile}.wav");
            if (File.Exists(target)) return;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith($"{DefaultSoundFile}.wav", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                DebugWindow.LogError($"[SoundNotifier] No embedded {DefaultSoundFile}.wav to extract.");
                return;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var file = File.Create(target);
            stream.CopyTo(file);
            DebugWindow.LogMsg($"[SoundNotifier] Wrote the default alert sound to {target}");
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"[SoundNotifier] Unable to write the default sound: {ex.Message}");
        }
    }

    public static void Process(IReadOnlyCollection<CustomItemData> items)
    {
        var settings = Main.Settings.SoundNotificationSettings;
        if (!settings.Enable) return;

        // Forget labels that are gone, so picking an item up and dropping it alerts again.
        var present = items.Select(x => x.LabelAddress).ToHashSet();
        Alerted.RemoveWhere(address => !present.Contains(address));

        foreach (var item in items)
        {
            if (Alerted.Contains(item.LabelAddress)) continue;
            if (!WantsSound(item, settings)) continue;

            var file = ResolveSoundFile(item);

            // Claim the label even with no file to play, otherwise a missing default.wav
            // would re-run this lookup for every item on every tick.
            if (!Alerted.Add(item.LabelAddress)) continue;
            if (file == null)
            {
                DebugWindow.LogError(
                    $"[SoundNotifier] No sound to play for {item.Name}. Put {DefaultSoundFile}.wav " +
                    $"in {Main.ConfigDirectory} and press Reload sound list.");
                continue;
            }

            Main.GameController.SoundController.PlaySound(file, settings.Volume);
        }
    }

    private static bool WantsSound(CustomItemData item, SoundNotificationSettings settings)
    {
        if (settings.PlayForHighlightedUniques && UniqueHighlightDisplay.Matches(item)) return true;

        return item.IsWanted == true && item.MatchedRule is { PlaySound: true };
    }

    /// <summary>
    ///     Most specific sound wins: a wav named after the unique, then one named after the rule
    ///     file that matched, then the default. Drop "Mageblood.wav" or "unique.wav" in the config
    ///     directory and it is picked up on the next reload.
    /// </summary>
    private static string ResolveSoundFile(CustomItemData item)
    {
        foreach (var candidate in item.UniqueNameCandidates)
            if (_soundFiles.TryGetValue(candidate, out var uniqueSound))
                return Existing(uniqueSound);

        var ruleLocation = item.MatchedRule?.Location;
        if (!string.IsNullOrEmpty(ruleLocation) &&
            _soundFiles.TryGetValue(Path.GetFileNameWithoutExtension(ruleLocation), out var ruleSound))
            return Existing(ruleSound);

        return _soundFiles.TryGetValue(DefaultSoundFile, out var defaultSound) ? Existing(defaultSound) : null;
    }

    /// <summary>Guards against a wav deleted since the last reload.</summary>
    private static string Existing(string path)
    {
        if (File.Exists(path)) return path;

        DebugWindow.LogError($"[SoundNotifier] {path} no longer exists, reload the sound list.");
        return null;
    }
}
