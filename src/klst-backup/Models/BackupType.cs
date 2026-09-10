namespace KlstBackup.Models;

/// <summary>The kind of backup a run produces.</summary>
public enum BackupType
{
    /// <summary>Copies every file in the source.</summary>
    Full,

    /// <summary>Copies only files changed since the last full backup.</summary>
    Differential
}
