using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KlstBackup.Models;

namespace KlstBackup.Services;

/// <summary>Loads and saves the application configuration as JSON.</summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigPath { get; }

    public ConfigService() : this(DefaultPath)
    {
    }

    public ConfigService(string path)
    {
        ConfigPath = path;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileBackup", "config.json");

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Options);
                if (config is not null)
                {
                    return config;
                }
            }
        }
        catch
        {
            // fall through to a fresh config; the corrupt file will be replaced on next save
        }

        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, Options));
        File.Move(temp, ConfigPath, overwrite: true);
    }
}
