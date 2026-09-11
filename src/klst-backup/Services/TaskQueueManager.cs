using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KlstBackup.Models;

namespace KlstBackup.Services;

public class TaskQueueManager
{
    private readonly List<BackupTask> _activeTasks = new();
    private readonly List<BackupTask> _queuedTasks = new();
    private readonly object _lock = new();

    public IReadOnlyList<BackupTask> ActiveTasks
    {
        get { lock (_lock) return _activeTasks.ToList(); }
    }

    public IReadOnlyList<BackupTask> QueuedTasks
    {
        get { lock (_lock) return _queuedTasks.ToList(); }
    }

    public int MaxConcurrentTasks => GetPhysicalDiskCount();

    public event Action<BackupTask>? TaskStarted;
    public event Action<BackupTask>? TaskCompleted;
    public event Action<BackupTask>? TaskFailed;

    public void EnqueueTask(BackupTask task)
    {
        lock (_lock)
        {
            if (CanStartTask(task))
            {
                StartTask(task);
            }
            else
            {
                _queuedTasks.Add(task);
                _queuedTasks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }
    }

    public void StartTask(BackupTask task)
    {
        lock (_lock)
        {
            _activeTasks.Add(task);
        }
        task.Start();
        TaskStarted?.Invoke(task);
    }

    public void PauseTask(Guid taskId)
    {
        lock (_lock)
        {
            var task = _activeTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                task.Pause();
            }
        }
    }

    public void ResumeTask(Guid taskId)
    {
        lock (_lock)
        {
            var task = _activeTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                task.Resume();
            }
        }
    }

    /// <summary>
    /// Removes a task that is still waiting in the queue (never started). Used by
    /// SchedulerService to back a manual run out when every disk slot is busy. Returns
    /// false when the task already started or is unknown.
    /// </summary>
    public bool TryRemoveQueued(Guid taskId)
    {
        lock (_lock)
        {
            var task = _queuedTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null)
            {
                return false;
            }

            _queuedTasks.Remove(task);
            return true;
        }
    }

    public void CancelTask(Guid taskId)
    {
        lock (_lock)
        {
            var task = _activeTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                task.Cancel();
                _activeTasks.Remove(task);
                ProcessQueue();
            }
        }
    }

    public void CompleteTask(Guid taskId)
    {
        lock (_lock)
        {
            var task = _activeTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                task.Complete();
                _activeTasks.Remove(task);
                TaskCompleted?.Invoke(task);
                ProcessQueue();
            }
        }
    }

    public void FailTask(Guid taskId)
    {
        lock (_lock)
        {
            var task = _activeTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                task.Fail();
                _activeTasks.Remove(task);
                TaskFailed?.Invoke(task);
                ProcessQueue();
            }
        }
    }

    private bool CanStartTask(BackupTask task)
    {
        if (_activeTasks.Count >= MaxConcurrentTasks)
            return false;

        var taskDisk = GetPhysicalDisk(task.Job.DestPath);
        foreach (var activeTask in _activeTasks)
        {
            var activeDisk = GetPhysicalDisk(activeTask.Job.DestPath);
            if (activeDisk == taskDisk)
                return false;
        }

        return true;
    }

    private void ProcessQueue()
    {
        lock (_lock)
        {
            for (int i = 0; i < _queuedTasks.Count; i++)
            {
                var queuedTask = _queuedTasks[i];
                if (CanStartTask(queuedTask))
                {
                    _queuedTasks.RemoveAt(i);
                    StartTask(queuedTask);
                    return;
                }
            }
        }
    }

    private int GetPhysicalDiskCount()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName)
            .Distinct()
            .Count();
        return Math.Max(1, drives);
    }

    private string GetPhysicalDisk(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return root.ToUpperInvariant();
    }
}
