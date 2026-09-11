using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KlstBackup.Models;

namespace KlstBackup.Services;

/// <summary>Reads and writes backup set manifests stored in AppData, and inspects backup sets.</summary>
public static class ManifestStore
{
    public const string FileName = "manifest.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Returns the AppData directory for storing manifests of a given job.</summary>
    public static string GetSetsFolder(Guid jobId)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileBackup", "sets", jobId.ToString("N"));
        Directory.CreateDirectory(appData);
        return appData;
    }

    /// <summary>Saves a manifest for a named backup set under the job's AppData folder.</summary>
    public static void Save(BackupManifest manifest, Guid jobId, string setName)
    {
        var setsFolder = GetSetsFolder(jobId);
        var path = Path.Combine(setsFolder, setName + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, Options));
    }

    /// <summary>Loads a manifest by set name from the job's AppData folder.</summary>
    public static BackupManifest? Load(Guid jobId, string setName)
    {
        var setsFolder = GetSetsFolder(jobId);
        var path = Path.Combine(setsFolder, setName + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path), Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the set name of the most recent Full backup set for the job,
    /// or null when the job has no full backup yet.
    /// </summary>
    public static string? FindLatestFullSet(Guid jobId)
    {
        var setsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileBackup", "sets", jobId.ToString("N"));
        if (!Directory.Exists(setsFolder))
        {
            return null;
        }

        string? best = null;
        var bestTime = DateTime.MinValue;
        foreach (var file in Directory.EnumerateFiles(setsFolder, "Full_*.json"))
        {
            var setName = Path.GetFileNameWithoutExtension(file);
            var manifest = Load(jobId, setName);
            var created = manifest?.CreatedUtc ?? ParseSetFolderName(setName) ?? DateTime.MinValue;
            if (created > bestTime)
            {
                bestTime = created;
                best = setName;
            }
        }

        return best;
    }

    /// <summary>All backup set names for the job, newest first.</summary>
    public static List<string> ListSets(Guid jobId)
    {
        var sets = new List<string>();
        var setsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileBackup", "sets", jobId.ToString("N"));
        if (!Directory.Exists(setsFolder))
        {
            return sets;
        }

        foreach (var file in Directory.EnumerateFiles(setsFolder, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("Full_", StringComparison.Ordinal) || name.StartsWith("Diff_", StringComparison.Ordinal))
            {
                sets.Add(name);
            }
        }

        sets.Sort((a, b) =>
        {
            var ta = ParseSetFolderName(a) ?? DateTime.MinValue;
            var tb = ParseSetFolderName(b) ?? DateTime.MinValue;
            return tb.CompareTo(ta);
        });
        return sets;
    }

    /// <summary>Parses "Full_20260910_120000" / "Diff_20260910_120000" into a local DateTime.</summary>
    public static DateTime? ParseSetFolderName(string setName)
    {
        var prefixLength = setName.StartsWith("Full_", StringComparison.Ordinal) ? 5
            : setName.StartsWith("Diff_", StringComparison.Ordinal) ? 5
            : 0;
        if (prefixLength == 0)
        {
            return null;
        }

        var stamp = setName[prefixLength..];

        // Strip the uniqueness suffix appended by the engine, e.g. "Full_20260910_120000_2".
        // "yyyyMMdd_HHmmss" is exactly 15 characters, so the suffix underscore sits at index 15.
        if (stamp.Length > 15 && stamp[15] == '_')
        {
            stamp = stamp[..15];
        }

        // Pairs with the write side in BackupEngine.RunBackup: the set folder stamp is always
        // invariant, so it must be read back invariant regardless of the ambient culture.
        if (DateTime.TryParseExact(stamp, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return value;
        }

        return null;
    }
}
