using System.Collections.Generic;

namespace KlstBackup.Models;

/// <summary>Persisted application configuration (%APPDATA%\FileBackup\config.json).</summary>
public class AppConfig
{
    public List<BackupJob> Jobs { get; set; } = new();

    public bool StartWithWindows { get; set; }
}
