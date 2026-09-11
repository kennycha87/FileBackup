using System;
using System.Collections.Generic;

namespace KlstBackup.Models;

public class CheckpointState
{
    public Guid TaskId { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Running;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string SourcePath { get; set; } = string.Empty;
    public string DestPath { get; set; } = string.Empty;
    public BackupType BackupType { get; set; } = BackupType.Full;
    public List<CheckpointFileEntry> Files { get; set; } = new();
    public int CurrentIndex { get; set; }
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public List<CheckpointError> Errors { get; set; } = new();
}

public class CheckpointFileEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string? Checksum { get; set; }
    public bool Copied { get; set; }
}

public class CheckpointError
{
    public string FilePath { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
