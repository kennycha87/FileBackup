using KlstBackup.Models;
using TaskStatus = KlstBackup.Models.TaskStatus;
using Xunit;

namespace KlstBackup.Tests;

public class BackupTaskTests
{
    [Fact]
    public void BackupTask_Start_SetsStatusToRunning()
    {
        var task = new BackupTask();
        task.Start();
        Assert.Equal(TaskStatus.Running, task.Status);
        Assert.True(task.Stopwatch.IsRunning);
    }

    [Fact]
    public void BackupTask_Pause_SetsStatusToPaused()
    {
        var task = new BackupTask();
        task.Start();
        task.Pause();
        Assert.Equal(TaskStatus.Paused, task.Status);
        Assert.True(task.IsPaused);
        Assert.False(task.Stopwatch.IsRunning);
    }

    [Fact]
    public void BackupTask_Resume_SetsStatusToRunning()
    {
        var task = new BackupTask();
        task.Start();
        task.Pause();
        task.Resume();
        Assert.Equal(TaskStatus.Running, task.Status);
        Assert.False(task.IsPaused);
        Assert.True(task.Stopwatch.IsRunning);
    }

    [Fact]
    public void BackupTask_Cancel_SetsStatusToCancelled()
    {
        var task = new BackupTask();
        task.Start();
        task.Cancel();
        Assert.Equal(TaskStatus.Cancelled, task.Status);
        Assert.True(task.IsCancelled);
    }

    [Fact]
    public void BackupTask_Complete_SetsStatusToCompleted()
    {
        var task = new BackupTask();
        task.Start();
        task.Complete();
        Assert.Equal(TaskStatus.Completed, task.Status);
    }

    [Fact]
    public void BackupTask_ProgressPercent_CalculatesCorrectly()
    {
        var task = new BackupTask { FilesProcessed = 50, TotalFiles = 100 };
        Assert.Equal(50.0, task.ProgressPercent);
    }

    [Fact]
    public void BackupTask_ProgressPercent_ZeroTotalFiles_ReturnsZero()
    {
        var task = new BackupTask { FilesProcessed = 50, TotalFiles = 0 };
        Assert.Equal(0, task.ProgressPercent);
    }
}
