namespace KlstBackup.Models;

/// <summary>Outcome of a single backup run.</summary>
public class BackupResult
{
    public bool Success { get; set; }

    public bool Cancelled { get; set; }

    /// <summary>The type actually executed (a differential may be promoted to full).</summary>
    public BackupType Type { get; set; }

    public int FilesCopied { get; set; }

    public long BytesCopied { get; set; }

    public int Warnings { get; set; }

    /// <summary>Folder name of the created backup set, e.g. "Full_20260910_120000".</summary>
    public string? SetFolder { get; set; }

    public string? Error { get; set; }

    public TimeSpan Duration { get; set; }
}
