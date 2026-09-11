using System.IO;
using KlstBackup.Models;
using KlstBackup.Services;
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

public class CheckpointStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly CheckpointStore _store;

    public CheckpointStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"checkpoint_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _store = new CheckpointStore(_testDir);
    }

    [Fact]
    public void SaveCheckpoint_CreatesFile()
    {
        var checkpoint = new CheckpointState { TaskId = Guid.NewGuid() };
        _store.SaveCheckpoint(checkpoint.TaskId, checkpoint);
        var loaded = _store.LoadCheckpoint(checkpoint.TaskId);
        Assert.NotNull(loaded);
        Assert.Equal(checkpoint.TaskId, loaded!.TaskId);
    }

    [Fact]
    public void LoadCheckpoint_NonExistent_ReturnsNull()
    {
        var result = _store.LoadCheckpoint(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public void DeleteCheckpoint_RemovesFile()
    {
        var checkpoint = new CheckpointState { TaskId = Guid.NewGuid() };
        _store.SaveCheckpoint(checkpoint.TaskId, checkpoint);
        _store.DeleteCheckpoint(checkpoint.TaskId);
        var loaded = _store.LoadCheckpoint(checkpoint.TaskId);
        Assert.Null(loaded);
    }

    [Fact]
    public void ListCheckpoints_ReturnsAllCheckpoints()
    {
        var checkpoint1 = new CheckpointState { TaskId = Guid.NewGuid() };
        var checkpoint2 = new CheckpointState { TaskId = Guid.NewGuid() };
        _store.SaveCheckpoint(checkpoint1.TaskId, checkpoint1);
        _store.SaveCheckpoint(checkpoint2.TaskId, checkpoint2);
        var checkpoints = _store.ListCheckpoints();
        Assert.Equal(2, checkpoints.Count);
    }

    [Fact]
    public void ValidateCheckpoint_ValidCheckpoint_ReturnsTrue()
    {
        var checkpoint = new CheckpointState
        {
            TaskId = Guid.NewGuid(),
            SourcePath = _testDir,
            DestPath = _testDir
        };
        _store.SaveCheckpoint(checkpoint.TaskId, checkpoint);
        Assert.True(_store.ValidateCheckpoint(checkpoint.TaskId));
    }

    [Fact]
    public void ValidateCheckpoint_InvalidPath_ReturnsFalse()
    {
        var checkpoint = new CheckpointState
        {
            TaskId = Guid.NewGuid(),
            SourcePath = "C:\\NonExistentPath",
            DestPath = "C:\\NonExistentPath"
        };
        _store.SaveCheckpoint(checkpoint.TaskId, checkpoint);
        Assert.False(_store.ValidateCheckpoint(checkpoint.TaskId));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
