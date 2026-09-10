using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KlstBackup.Models;
using KlstBackup.Services;

namespace KlstBackup.ViewModels;

/// <summary>Wraps a <see cref="BackupJob"/> for display in the job list.</summary>
public partial class JobItemViewModel : ObservableObject
{
    private readonly Action _onConfigChanged;

    public BackupJob Job { get; }

    public JobItemViewModel(BackupJob job, Action onConfigChanged)
    {
        Job = job;
        _onConfigChanged = onConfigChanged;
    }

    public string Name => Job.Name;

    public string ScheduleSummary => SchedulerMath.Describe(Job);

    public string SourcePath => Job.SourcePath;

    public string DestPath => Job.DestPath;

    public string JobType => Job.JobType == BackupType.Full ? "Full" : "Differential";

    public string LastRunText => Job.LastRunUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public string NextRunText
    {
        get
        {
            if (!Job.Enabled)
            {
                return "-";
            }

            if (SchedulerMath.IsDue(Job, DateTime.Now))
            {
                return "Due now";
            }

            return SchedulerMath.ComputeNextRun(Job, DateTime.Now).ToString("yyyy-MM-dd HH:mm");
        }
    }

    public bool Enabled
    {
        get => Job.Enabled;
        set
        {
            if (Job.Enabled == value)
            {
                return;
            }

            Job.Enabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NextRunText));
            _onConfigChanged();
        }
    }

    /// <summary>Refreshes all displayed values after the underlying job was edited.</summary>
    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }
}
