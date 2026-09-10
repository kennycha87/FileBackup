namespace KlstBackup.Models;

/// <summary>Progress snapshot reported while a backup or restore runs.</summary>
public class BackupProgress
{
    public string CurrentFile { get; set; } = string.Empty;

    public int FilesDone { get; set; }

    public int TotalFiles { get; set; }

    public long BytesCopied { get; set; }

    public long TotalBytes { get; set; }

    public string? Warning { get; set; }
}
