using System.IO;
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;
using TaskStatus = KlstBackup.Models.TaskStatus;

namespace KlstBackup.Tests;

public class TaskStatusTests
{
    [Fact]
    public void TaskStatus_HasAllExpectedValues()
    {
        var values = Enum.GetValues<TaskStatus>();
        Assert.Contains(TaskStatus.Queued, values);
        Assert.Contains(TaskStatus.Running, values);
        Assert.Contains(TaskStatus.Paused, values);
        Assert.Contains(TaskStatus.Completed, values);
        Assert.Contains(TaskStatus.Failed, values);
        Assert.Contains(TaskStatus.Cancelled, values);
    }

    [Fact]
    public void TaskPriority_HasCorrectOrdering()
    {
        Assert.True(TaskPriority.Low < TaskPriority.Normal);
        Assert.True(TaskPriority.Normal < TaskPriority.High);
    }
}

public class TaskQueueManagerTests
{
    [Fact]
    public void EnqueueTask_EmptyQueue_StartsTask()
    {
        var manager = new TaskQueueManager();
        var task = CreateTestTask();
        manager.EnqueueTask(task);
        Assert.Equal(TaskStatus.Running, task.Status);
        Assert.Single(manager.ActiveTasks);
    }

    [Fact]
    public void PauseTask_RunningTask_SetsPausedStatus()
    {
        var manager = new TaskQueueManager();
        var task = CreateTestTask();
        manager.EnqueueTask(task);
        manager.PauseTask(task.TaskId);
        Assert.Equal(TaskStatus.Paused, task.Status);
    }

    [Fact]
    public void ResumeTask_PausedTask_SetsRunningStatus()
    {
        var manager = new TaskQueueManager();
        var task = CreateTestTask();
        manager.EnqueueTask(task);
        manager.PauseTask(task.TaskId);
        manager.ResumeTask(task.TaskId);
        Assert.Equal(TaskStatus.Running, task.Status);
    }

    [Fact]
    public void CancelTask_RunningTask_RemovesFromActive()
    {
        var manager = new TaskQueueManager();
        var task = CreateTestTask();
        manager.EnqueueTask(task);
        manager.CancelTask(task.TaskId);
        Assert.Empty(manager.ActiveTasks);
        Assert.Equal(TaskStatus.Cancelled, task.Status);
    }

    [Fact]
    public void CompleteTask_RunningTask_RemovesFromActive()
    {
        var manager = new TaskQueueManager();
        var task = CreateTestTask();
        manager.EnqueueTask(task);
        manager.CompleteTask(task.TaskId);
        Assert.Empty(manager.ActiveTasks);
        Assert.Equal(TaskStatus.Completed, task.Status);
    }

    private BackupTask CreateTestTask()
    {
        return new BackupTask
        {
            Job = new BackupJob
            {
                Name = "Test Job",
                SourcePath = Path.GetTempPath(),
                DestPath = Path.GetTempPath()
            }
        };
    }
}
