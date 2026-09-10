namespace KlstBackup.Models;

public enum RunStatus
{
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled
}

/// <summary>One entry in a job's run history.</summary>
public class RunRecord
{
    public string JobName { get; set; } = string.Empty;

    public DateTime StartedUtc { get; set; }

    public BackupType Type { get; set; }

    public RunStatus Status { get; set; }

    public int FilesCopied { get; set; }

    public long BytesCopied { get; set; }

    public TimeSpan Duration { get; set; }

    /// <summary>Backup set folder name on success, otherwise the failure reason.</summary>
    public string Message { get; set; } = string.Empty;
}
