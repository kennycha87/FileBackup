using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KlstBackup.Models;
using KlstBackup.Services;

namespace KlstBackup.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly TaskQueueManager _taskQueueManager;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<TaskItemViewModel> ActiveTasks { get; } = new();

    [ObservableProperty]
    private bool _showResourceMonitor;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private long _memoryUsage;

    public DashboardViewModel(TaskQueueManager taskQueueManager)
    {
        _taskQueueManager = taskQueueManager;

        _taskQueueManager.TaskStarted += OnTaskStarted;
        _taskQueueManager.TaskCompleted += OnTaskCompleted;
        _taskQueueManager.TaskFailed += OnTaskFailed;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += OnRefreshTimer;
        _refreshTimer.Start();

        RefreshTasks();
    }

    private void OnRefreshTimer(object? sender, EventArgs e)
    {
        RefreshTasks();
    }

    private void RefreshTasks()
    {
        var activeTasks = _taskQueueManager.ActiveTasks;

        // Update existing tasks
        foreach (var taskVm in ActiveTasks.ToList())
        {
            var task = activeTasks.FirstOrDefault(t => t.TaskId == taskVm.Task.TaskId);
            if (task != null)
            {
                taskVm.Refresh();
            }
            else
            {
                ActiveTasks.Remove(taskVm);
            }
        }

        // Add new tasks
        foreach (var task in activeTasks)
        {
            if (!ActiveTasks.Any(vm => vm.Task.TaskId == task.TaskId))
            {
                ActiveTasks.Add(new TaskItemViewModel(task));
            }
        }
    }

    private void OnTaskStarted(BackupTask task)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ActiveTasks.Add(new TaskItemViewModel(task));
        });
    }

    private void OnTaskCompleted(BackupTask task)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var taskVm = ActiveTasks.FirstOrDefault(vm => vm.Task.TaskId == task.TaskId);
            if (taskVm != null)
            {
                ActiveTasks.Remove(taskVm);
            }
        });
    }

    private void OnTaskFailed(BackupTask task)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var taskVm = ActiveTasks.FirstOrDefault(vm => vm.Task.TaskId == task.TaskId);
            if (taskVm != null)
            {
                ActiveTasks.Remove(taskVm);
            }
        });
    }

    [RelayCommand]
    private void PauseTask(TaskItemViewModel taskVm)
    {
        _taskQueueManager.PauseTask(taskVm.Task.TaskId);
    }

    [RelayCommand]
    private void ResumeTask(TaskItemViewModel taskVm)
    {
        _taskQueueManager.ResumeTask(taskVm.Task.TaskId);
    }

    [RelayCommand]
    private void CancelTask(TaskItemViewModel taskVm)
    {
        _taskQueueManager.CancelTask(taskVm.Task.TaskId);
    }

    [RelayCommand]
    private void ToggleResourceMonitor()
    {
        ShowResourceMonitor = !ShowResourceMonitor;
    }
}
