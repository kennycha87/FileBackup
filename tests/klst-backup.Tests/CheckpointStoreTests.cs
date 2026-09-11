using KlstBackup.Models;
using Xunit;

namespace KlstBackup.Tests;

public class CheckpointStateTests
{
    [Fact]
    public void CheckpointState_DefaultValues_AreCorrect()
    {
        var checkpoint = new CheckpointState();
        Assert.Equal(KlstBackup.Models.TaskStatus.Running, checkpoint.Status);
        Assert.Equal(BackupType.Full, checkpoint.BackupType);
        Assert.NotNull(checkpoint.Files);
        Assert.Empty(checkpoint.Files);
        Assert.NotNull(checkpoint.Errors);
        Assert.Empty(checkpoint.Errors);
    }

    [Fact]
    public void CheckpointFileEntry_Properties_WorkCorrectly()
    {
        var entry = new CheckpointFileEntry
        {
            RelativePath = "test/file.txt",
            Size = 1024,
            Copied = true
        };
        Assert.Equal("test/file.txt", entry.RelativePath);
        Assert.Equal(1024, entry.Size);
        Assert.True(entry.Copied);
    }
}
