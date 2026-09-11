using System.Collections.Generic;

namespace KlstBackup.Models;

/// <summary>Persisted application configuration (%APPDATA%\FileBackup\config.json).</summary>
public class AppConfig
{
    public List<BackupJob> Jobs { get; set; } = new();

    public bool StartWithWindows { get; set; }

    /// <summary>UI language tag ("zh-HK", "zh-CN"). Null/empty = follow the Windows display language.</summary>
    public string? Language { get; set; }
}
