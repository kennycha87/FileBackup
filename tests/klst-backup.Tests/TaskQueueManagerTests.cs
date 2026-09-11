using KlstBackup.Models;
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
