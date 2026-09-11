using System;
using System.Diagnostics;

namespace KlstBackup.Models;

public class BackupTask
{
    public Guid TaskId { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public BackupJob Job { get; set; } = null!;
    public TaskStatus Status { get; set; } = TaskStatus.Queued;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DateTime StartTime { get; set; }
    public TimeSpan ElapsedTime => Stopwatch.Elapsed;
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public CheckpointState? Checkpoint { get; set; }
    public Stopwatch Stopwatch { get; } = new();
    public bool IsPaused { get; set; }
    public bool IsCancelled { get; set; }

    public double ProgressPercent => TotalFiles > 0 ? FilesProcessed * 100.0 / TotalFiles : 0;

    public void Start()
    {
        Status = TaskStatus.Running;
        StartTime = DateTime.UtcNow;
        Stopwatch.Start();
    }

    public void Pause()
    {
        IsPaused = true;
        Status = TaskStatus.Paused;
        Stopwatch.Stop();
    }

    public void Resume()
    {
        IsPaused = false;
        Status = TaskStatus.Running;
        Stopwatch.Start();
    }

    public void Cancel()
    {
        IsCancelled = true;
        Status = TaskStatus.Cancelled;
        Stopwatch.Stop();
    }

    public void Complete()
    {
        Status = TaskStatus.Completed;
        Stopwatch.Stop();
    }

    public void Fail()
    {
        Status = TaskStatus.Failed;
        Stopwatch.Stop();
    }
}
