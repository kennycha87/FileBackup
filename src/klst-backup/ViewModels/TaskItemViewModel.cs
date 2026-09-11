using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KlstBackup.Models;
// Disambiguate from System.Threading.Tasks.TaskStatus (implicit usings)
using TaskStatus = KlstBackup.Models.TaskStatus;

namespace KlstBackup.ViewModels;

public partial class TaskItemViewModel : ObservableObject
{
    private readonly BackupTask _task;

    [ObservableProperty]
    private string _jobName;

    [ObservableProperty]
    private TaskStatus _status;

    [ObservableProperty]
    private TaskPriority _priority;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText;

    [ObservableProperty]
    private string _elapsedTime;

    [ObservableProperty]
    private string _currentFile;

    [ObservableProperty]
    private bool _isPaused;

    public BackupTask Task => _task;

    public TaskItemViewModel(BackupTask task)
    {
        _task = task;
        JobName = task.Job.Name;
        Status = task.Status;
        Priority = task.Priority;
        ProgressPercent = task.ProgressPercent;
        UpdateProgressText();
    }

    public void Refresh()
    {
        Status = _task.Status;
        ProgressPercent = _task.ProgressPercent;
        ElapsedTime = _task.ElapsedTime.ToString(@"hh\:mm\:ss");
        CurrentFile = _task.CurrentFile;
        IsPaused = _task.IsPaused;
        UpdateProgressText();
    }

    private void UpdateProgressText()
    {
        ProgressText = $"{_task.FilesProcessed}/{_task.TotalFiles} files ({ProgressPercent:F1}%)";
    }
}
