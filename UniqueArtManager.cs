using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExileCore;
using Newtonsoft.Json;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

public class UniqueArtManager()
{
    public static Dictionary<string, List<string>> GetGameFileUniqueArtMapping()
    {
        if (Main.GameController.Files.UniqueItemDescriptions.EntriesList.Count == 0)
            Main.GameController.Files.LoadFiles();

        return Main.GameController
            .Files.ItemVisualIdentities.EntriesList
            .Where(x => x.ArtPath != null)
            .GroupJoin(
                Main.GameController.Files.UniqueItemDescriptions.EntriesList
                    .Where(x => x.ItemVisualIdentity != null),
                x => x,
                x => x.ItemVisualIdentity,
                (ivi, descriptions) => (ivi.ArtPath, descriptions: descriptions.ToList())
            )
            .GroupBy(x => x.ArtPath, x => x.descriptions)
            .Select(x => (x.Key, Names: x
                .SelectMany(items => items)
                .Select(item => item.UniqueName?.Text)
                .Where(name => name != null)
                .Distinct()
                .ToList()))
            .Where(x => x.Names.Count != 0)
            .ToDictionary(x => x.Key, x => x.Names);
    }

    public static Dictionary<string, List<string>> LoadUniqueArtMapping(bool ignoreGameMapping)
    {
        Dictionary<string, List<string>> mapping = null;

        if (!ignoreGameMapping)
            try
            {
                var files = Main.GameController.Files;

                // Previously this bailed out whenever the lists were empty, which is exactly the
                // state before the first area is entered. Load them instead, the way the rebuild
                // button already does, so the mapping is available from plugin start.
                if (files.UniqueItemDescriptions.EntriesList.Count == 0 ||
                    files.ItemVisualIdentities.EntriesList.Count == 0)
                    files.LoadFiles();

                if (files.UniqueItemDescriptions.EntriesList.Count != 0 &&
                    files.ItemVisualIdentities.EntriesList.Count != 0)
                    mapping = GetGameFileUniqueArtMapping();
            }
            catch (Exception ex)
            {
                LogError($"Unable to read the art mapping from game files, falling back: {ex.Message}");
            }

        var customFilePath = Path.Join(Main.DirectoryFullName, CustomUniqueArtMappingPath);

        if (File.Exists(customFilePath))
            try
            {
                mapping ??= JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(
                    File.ReadAllText(customFilePath)
                );
            }
            catch (Exception ex)
            {
                Main.LogError($"Unable to load custom art mapping: {ex}");
            }

        mapping ??= GetEmbeddedUniqueArtMapping();
        mapping ??= [];
        return mapping;
    }

    private static Dictionary<string, List<string>> GetEmbeddedUniqueArtMapping()
    {
        try
        {
            // MSBuild prefixes embedded resources with the root namespace, so the resource is
            // really "Ground_Items_With_Linq.uniqueArtMapping.default.json". Asking for the bare
            // file name returns null, which silently disabled this fallback entirely. Resolve
            // by suffix so a namespace or file rename cannot break it again.
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith(DefaultUniqueArtMappingPath, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                LogError(
                    $"No embedded resource ending in {DefaultUniqueArtMappingPath}. " +
                    $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                LogError($"Embedded stream {resourceName} could not be opened");
                return null;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(content);
        }
        catch (Exception ex)
        {
            LogError($"Unable to load embedded art mapping: {ex}");
            return null;
        }
    }

    private static void LogError(string message)
    {
        DebugWindow.LogError($"[UniqueArtManager] {message}");
    }

    private static void LogMessage(string message)
    {
        DebugWindow.LogMsg($"[UniqueArtManager] {message}");
    }
}