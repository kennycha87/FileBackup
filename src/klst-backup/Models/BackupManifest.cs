using System.Collections.Generic;

namespace KlstBackup.Models;

/// <summary>
/// Catalog of one backup set (a Full_ or Diff_ folder in the destination).
/// The manifest of a full backup is the baseline used by later differential backups.
/// </summary>
public class BackupManifest
{
    public string JobName { get; set; } = string.Empty;

    public BackupType Type { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Folder name of the full backup set this differential is based on.</summary>
    public string? BaseFullSet { get; set; }

    public List<FileEntry> Files { get; set; } = new();
}
