namespace KlstBackup.Models;

/// <summary>Outcome of a restore operation.</summary>
public class RestoreResult
{
    public bool Success { get; set; }

    public bool Cancelled { get; set; }

    public int FilesRestored { get; set; }

    public long BytesRestored { get; set; }

    public int Warnings { get; set; }

    public string? Error { get; set; }

    public TimeSpan Duration { get; set; }
}
