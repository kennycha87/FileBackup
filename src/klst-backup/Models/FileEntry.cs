namespace KlstBackup.Models;

/// <summary>One file tracked in a backup manifest.</summary>
public class FileEntry
{
    /// <summary>Path relative to the source root, using forward slashes.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }
}
