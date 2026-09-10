namespace KlstBackup.Models;

/// <summary>A user-configured backup job.</summary>
public class BackupJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string DestPath { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;

    /// <summary>Local time of day the backup should run.</summary>
    public TimeOnly Time { get; set; } = new(2, 0);

    /// <summary>Target weekday. Only used when <see cref="ScheduleType"/> is Weekly.</summary>
    public DayOfWeek? WeekDay { get; set; } = DayOfWeek.Sunday;

    /// <summary>Day of month (1-31, clamped to month length). Only used when ScheduleType is Monthly.</summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>Kind of backup produced by scheduled and manual runs.</summary>
    public BackupType JobType { get; set; } = BackupType.Full;

    /// <summary>When the last scheduled run started (UTC). Manual runs do not update this.</summary>
    public DateTime? LastRunUtc { get; set; }

    public List<RunRecord> History { get; set; } = new();

    /// <summary>Creates a deep-enough copy used for editing in the job dialog.</summary>
    public BackupJob Clone() => new()
    {
        Id = Id,
        Name = Name,
        SourcePath = SourcePath,
        DestPath = DestPath,
        Enabled = Enabled,
        ScheduleType = ScheduleType,
        Time = Time,
        WeekDay = WeekDay,
        DayOfMonth = DayOfMonth,
        JobType = JobType,
        LastRunUtc = LastRunUtc,
        History = new List<RunRecord>(History)
    };

    /// <summary>Copies the user-editable fields from another job instance.</summary>
    public void CopyEditableFrom(BackupJob other)
    {
        Name = other.Name;
        SourcePath = other.SourcePath;
        DestPath = other.DestPath;
        Enabled = other.Enabled;
        ScheduleType = other.ScheduleType;
        Time = other.Time;
        WeekDay = other.WeekDay;
        DayOfMonth = other.DayOfMonth;
        JobType = other.JobType;
    }
}
