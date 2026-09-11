using System;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using KlstBackup.Models;
using KlstBackup.Resources;

namespace KlstBackup.ViewModels;

/// <summary>Editable view model backing the add/edit job dialog.</summary>
public partial class JobEditViewModel : ObservableObject
{
    /// <summary>The job instance being edited (a clone for edits, a new instance for additions).</summary>
    public BackupJob Job { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _destPath = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private ScheduleType _scheduleType = ScheduleType.Daily;

    [ObservableProperty]
    private string _timeText = "02:00";

    [ObservableProperty]
    private DayOfWeek _weekDay = DayOfWeek.Sunday;

    [ObservableProperty]
    private int _dayOfMonth = 1;

    [ObservableProperty]
    private BackupType _jobType = BackupType.Full;

    [ObservableProperty]
    private string? _error;

    public JobEditViewModel(BackupJob job)
    {
        Job = job;
        _name = job.Name;
        _sourcePath = job.SourcePath;
        _destPath = job.DestPath;
        _enabled = job.Enabled;
        _scheduleType = job.ScheduleType;
        // "HH:mm" is the documented input contract (see Edit_AtToolTip). Both the write and the
        // read side are pinned to the invariant culture so a locale whose TimeSeparator is not
        // ":" cannot break the round trip.
        _timeText = job.Time.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        _weekDay = job.WeekDay ?? DayOfWeek.Sunday;
        _dayOfMonth = job.DayOfMonth;
        _jobType = job.JobType;
    }

    /// <summary>Validates the input and applies it to <see cref="Job"/>. Returns false with a message in Error.</summary>
    public bool Accept()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = Strings.Err_NameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            Error = Strings.Err_SourceMissing;
            return false;
        }

        if (string.IsNullOrWhiteSpace(DestPath))
        {
            Error = Strings.Err_DestRequired;
            return false;
        }

        if (!TimeOnly.TryParseExact(TimeText.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            Error = Strings.Err_TimeFormat;
            return false;
        }

        Job.Name = Name.Trim();
        Job.SourcePath = SourcePath.Trim();
        Job.DestPath = DestPath.Trim();
        Job.Enabled = Enabled;
        Job.ScheduleType = ScheduleType;
        Job.Time = time;
        Job.WeekDay = WeekDay;
        Job.DayOfMonth = DayOfMonth;
        Job.JobType = JobType;
        Error = null;
        return true;
    }
}
