# Background Task Management System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a background task management system for long-running backup operations with pause/resume capabilities, persistent checkpoints, and a dedicated floating dashboard window.

**Architecture:** Task Queue Architecture with hybrid storage (SQLite for job metadata, JSON for checkpoint state). Multiple concurrent backups with one-per-physical-disk concurrency control. Separate floating dashboard window with real-time updates.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, SQLite (Microsoft.Data.Sqlite), JSON (System.Text.Json)

---

## File Structure

### New Files to Create:
- `src/klst-backup/Models/BackupTask.cs` - Individual backup task with pause/resume
- `src/klst-backup/Models/CheckpointState.cs` - Checkpoint data structure
- `src/klst-backup/Models/TaskStatus.cs` - Task status enum
- `src/klst-backup/Models/TaskPriority.cs` - Task priority enum
- `src/klst-backup/Services/TaskQueueManager.cs` - Central task coordinator
- `src/klst-backup/Services/CheckpointStore.cs` - Hybrid storage for checkpoints
- `src/klst-backup/Services/ResourceMonitor.cs` - Optional system resource tracking
- `src/klst-backup/ViewModels/DashboardViewModel.cs` - Dashboard window view model
- `src/klst-backup/ViewModels/TaskItemViewModel.cs` - Individual task view model
- `src/klst-backup/Views/DashboardWindow.xaml` - Dashboard window UI
- `src/klst-backup/Views/DashboardWindow.xaml.cs` - Dashboard window code-behind
- `tests/klst-backup.Tests/TaskQueueManagerTests.cs` - Task queue tests
- `tests/klst-backup.Tests/CheckpointStoreTests.cs` - Checkpoint store tests
- `tests/klst-backup.Tests/BackupTaskTests.cs` - Backup task tests

### Modified Files:
- `src/klst-backup/Services/BackupEngine.cs` - Add pause/resume support
- `src/klst-backup/Services/SchedulerService.cs` - Integrate with TaskQueueManager
- `src/klst-backup/ViewModels/MainViewModel.cs` - Update for concurrency, add dashboard launch
- `src/klst-backup/App.xaml.cs` - Initialize new services
- `src/klst-backup/klst-backup.csproj` - Add SQLite NuGet package

---

### Task 1: Project Setup and Dependencies

**Files:**
- Modify: `src/klst-backup/klst-backup.csproj`

- [ ] **Step 1: Add SQLite NuGet package**

Add to `klst-backup.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
</ItemGroup>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore src/klst-backup/klst-backup.csproj`
Expected: Packages restored successfully

- [ ] **Step 3: Commit**

```bash
git add src/klst-backup/klst-backup.csproj
git commit -m "chore: add SQLite dependency for checkpoint storage"
```

---

### Task 2: Task Status and Priority Enums

**Files:**
- Create: `src/klst-backup/Models/TaskStatus.cs`
- Create: `src/klst-backup/Models/TaskPriority.cs`
- Test: `tests/klst-backup.Tests/TaskQueueManagerTests.cs` (partial)

- [ ] **Step 1: Create TaskStatus enum**

Create `src/klst-backup/Models/TaskStatus.cs`:
```csharp
namespace KlstBackup.Models;

public enum TaskStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
```

- [ ] **Step 2: Create TaskPriority enum**

Create `src/klst-backup/Models/TaskPriority.cs`:
```csharp
namespace KlstBackup.Models;

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
```

- [ ] **Step 3: Write tests for enums**

Add to `tests/klst-backup.Tests/TaskQueueManagerTests.cs`:
```csharp
using KlstBackup.Models;
using Xunit;

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
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "TaskStatusTests|TaskPriorityTests"`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add src/klst-backup/Models/TaskStatus.cs src/klst-backup/Models/TaskPriority.cs tests/klst-backup.Tests/TaskQueueManagerTests.cs
git commit -m "feat: add TaskStatus and TaskPriority enums"
```

---

### Task 3: CheckpointState Model

**Files:**
- Create: `src/klst-backup/Models/CheckpointState.cs`
- Test: `tests/klst-backup.Tests/CheckpointStoreTests.cs` (partial)

- [ ] **Step 1: Create CheckpointState model**

Create `src/klst-backup/Models/CheckpointState.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace KlstBackup.Models;

public class CheckpointState
{
    public Guid TaskId { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Running;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string SourcePath { get; set; } = string.Empty;
    public string DestPath { get; set; } = string.Empty;
    public BackupType BackupType { get; set; } = BackupType.Full;
    public List<CheckpointFileEntry> Files { get; set; } = new();
    public int CurrentIndex { get; set; }
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public List<CheckpointError> Errors { get; set; } = new();
}

public class CheckpointFileEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string? Checksum { get; set; }
    public bool Copied { get; set; }
}

public class CheckpointError
{
    public string FilePath { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Write tests for CheckpointState**

Add to `tests/klst-backup.Tests/CheckpointStoreTests.cs`:
```csharp
using KlstBackup.Models;
using Xunit;

namespace KlstBackup.Tests;

public class CheckpointStateTests
{
    [Fact]
    public void CheckpointState_DefaultValues_AreCorrect()
    {
        var checkpoint = new CheckpointState();
        Assert.Equal(TaskStatus.Running, checkpoint.Status);
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
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "CheckpointStateTests"`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/klst-backup/Models/CheckpointState.cs tests/klst-backup.Tests/CheckpointStoreTests.cs
git commit -m "feat: add CheckpointState model for persistent checkpoints"
```

---

### Task 4: BackupTask Model

**Files:**
- Create: `src/klst-backup/Models/BackupTask.cs`
- Test: `tests/klst-backup.Tests/BackupTaskTests.cs`

- [ ] **Step 1: Create BackupTask model**

Create `src/klst-backup/Models/BackupTask.cs`:
```csharp
using System;
using System.Diagnostics;

namespace KlstBackup.Models;

public class BackupTask
{
    public Guid TaskId { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public BackupJob Job { get; set; } = null!;
    public TaskStatus Status { get; set; } = TaskStatus.Queued;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DateTime StartTime { get; set; }
    public TimeSpan ElapsedTime => Stopwatch.Elapsed;
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public CheckpointState? Checkpoint { get; set; }
    public Stopwatch Stopwatch { get; } = new();
    public bool IsPaused { get; set; }
    public bool IsCancelled { get; set; }

    public double ProgressPercent => TotalFiles > 0 ? FilesProcessed * 100.0 / TotalFiles : 0;

    public void Start()
    {
        Status = TaskStatus.Running;
        StartTime = DateTime.UtcNow;
        Stopwatch.Start();
    }

    public void Pause()
    {
        IsPaused = true;
        Status = TaskStatus.Paused;
        Stopwatch.Stop();
    }

    public void Resume()
    {
        IsPaused = false;
        Status = TaskStatus.Running;
        Stopwatch.Start();
    }

    public void Cancel()
    {
        IsCancelled = true;
        Status = TaskStatus.Cancelled;
        Stopwatch.Stop();
    }

    public void Complete()
    {
        Status = TaskStatus.Completed;
        Stopwatch.Stop();
    }

    public void Fail()
    {
        Status = TaskStatus.Failed;
        Stopwatch.Stop();
    }
}
```

- [ ] **Step 2: Write tests for BackupTask**

Create `tests/klst-backup.Tests/BackupTaskTests.cs`:
```csharp
using KlstBackup.Models;
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
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "BackupTaskTests"`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/klst-backup/Models/BackupTask.cs tests/klst-backup.Tests/BackupTaskTests.cs
git commit -m "feat: add BackupTask model with pause/resume support"
```

---

### Task 5: CheckpointStore Service

**Files:**
- Create: `src/klst-backup/Services/CheckpointStore.cs`
- Test: `tests/klst-backup.Tests/CheckpointStoreTests.cs`

- [ ] **Step 1: Create CheckpointStore service**

Create `src/klst-backup/Services/CheckpointStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KlstBackup.Models;

namespace KlstBackup.Services;

public class CheckpointStore
{
    private readonly string _checkpointDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public CheckpointStore(string appDataPath)
    {
        _checkpointDir = Path.Combine(appDataPath, "checkpoints");
        Directory.CreateDirectory(_checkpointDir);
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public void SaveCheckpoint(Guid taskId, CheckpointState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        var path = GetCheckpointPath(taskId);
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public CheckpointState? LoadCheckpoint(Guid taskId)
    {
        var path = GetCheckpointPath(taskId);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CheckpointState>(json);
    }

    public void DeleteCheckpoint(Guid taskId)
    {
        var path = GetCheckpointPath(taskId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public List<CheckpointState> ListCheckpoints()
    {
        var checkpoints = new List<CheckpointState>();
        foreach (var file in Directory.GetFiles(_checkpointDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json);
                if (checkpoint != null)
                    checkpoints.Add(checkpoint);
            }
            catch
            {
                // Skip corrupted checkpoints
            }
        }
        return checkpoints;
    }

    public bool ValidateCheckpoint(Guid taskId)
    {
        var checkpoint = LoadCheckpoint(taskId);
        if (checkpoint == null)
            return false;

        // Check required fields
        if (checkpoint.TaskId == Guid.Empty)
            return false;
        if (checkpoint.Files == null)
            return false;
        if (string.IsNullOrEmpty(checkpoint.SourcePath))
            return false;
        if (string.IsNullOrEmpty(checkpoint.DestPath))
            return false;

        // Check paths exist
        if (!Directory.Exists(checkpoint.SourcePath))
            return false;
        if (!Directory.Exists(checkpoint.DestPath))
            return false;

        return true;
    }

    public void CleanupOldCheckpoints(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var file in Directory.GetFiles(_checkpointDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json);
                if (checkpoint != null && checkpoint.UpdatedAt < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // Skip corrupted checkpoints
            }
        }
    }

    private string GetCheckpointPath(Guid taskId)
    {
        return Path.Combine(_checkpointDir, $"{taskId}.json");
    }
}
```

- [ ] **Step 2: Write tests for CheckpointStore**

Add to `tests/klst-backup.Tests/CheckpointStoreTests.cs`:
```csharp
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

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
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "CheckpointStoreTests"`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/klst-backup/Services/CheckpointStore.cs tests/klst-backup.Tests/CheckpointStoreTests.cs
git commit -m "feat: add CheckpointStore service for persistent checkpoint storage"
```

---

### Task 6: TaskQueueManager Service

**Files:**
- Create: `src/klst-backup/Services/TaskQueueManager.cs`
- Test: `tests/klst-backup.Tests/TaskQueueManagerTests.cs`

- [ ] **Step 1: Create TaskQueueManager service**

Create `src/klst-backup/Services/TaskQueueManager.cs`:
```csharp
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
```

- [ ] **Step 2: Write tests for TaskQueueManager**

Add to `tests/klst-backup.Tests/TaskQueueManagerTests.cs`:
```csharp
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

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
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "TaskQueueManagerTests"`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/klst-backup/Services/TaskQueueManager.cs tests/klst-backup.Tests/TaskQueueManagerTests.cs
git commit -m "feat: add TaskQueueManager service for concurrent backup coordination"
```

---

### Task 7: ResourceMonitor Service

**Files:**
- Create: `src/klst-backup/Services/ResourceMonitor.cs`
- Test: `tests/klst-backup.Tests/ResourceMonitorTests.cs`

- [ ] **Step 1: Create ResourceMonitor service**

Create `src/klst-backup/Services/ResourceMonitor.cs`:
```csharp
using System;
using System.Diagnostics;

namespace KlstBackup.Services;

public class ResourceMonitor
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;

    public ResourceMonitor()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
    }

    public double GetCpuUsage()
    {
        return _cpuCounter.NextValue();
    }

    public long GetMemoryUsage()
    {
        return GC.GetTotalMemory(false) / (1024 * 1024);
    }

    public long GetAvailableMemory()
    {
        return (long)_memoryCounter.NextValue() * 1024 * 1024;
    }
}
```

- [ ] **Step 2: Write tests for ResourceMonitor**

Create `tests/klst-backup.Tests/ResourceMonitorTests.cs`:
```csharp
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

public class ResourceMonitorTests
{
    [Fact]
    public void GetCpuUsage_ReturnsNonNegative()
    {
        var monitor = new ResourceMonitor();
        var cpu = monitor.GetCpuUsage();
        Assert.True(cpu >= 0);
    }

    [Fact]
    public void GetMemoryUsage_ReturnsPositive()
    {
        var monitor = new ResourceMonitor();
        var memory = monitor.GetMemoryUsage();
        Assert.True(memory > 0);
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/klst-backup.Tests --filter "ResourceMonitorTests"`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/klst-backup/Services/ResourceMonitor.cs tests/klst-backup.Tests/ResourceMonitorTests.cs
git commit -m "feat: add ResourceMonitor service for system resource tracking"
```

---

### Task 8: DashboardViewModel

**Files:**
- Create: `src/klst-backup/ViewModels/DashboardViewModel.cs`
- Create: `src/klst-backup/ViewModels/TaskItemViewModel.cs`

- [ ] **Step 1: Create TaskItemViewModel**

Create `src/klst-backup/ViewModels/TaskItemViewModel.cs`:
```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KlstBackup.Models;

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
```

- [ ] **Step 2: Create DashboardViewModel**

Create `src/klst-backup/ViewModels/DashboardViewModel.cs`:
```csharp
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
```

- [ ] **Step 3: Commit**

```bash
git add src/klst-backup/ViewModels/DashboardViewModel.cs src/klst-backup/ViewModels/TaskItemViewModel.cs
git commit -m "feat: add DashboardViewModel for task monitoring"
```

---

### Task 9: DashboardWindow UI

**Files:**
- Create: `src/klst-backup/Views/DashboardWindow.xaml`
- Create: `src/klst-backup/Views/DashboardWindow.xaml.cs`

- [ ] **Step 1: Create DashboardWindow XAML**

Create `src/klst-backup/Views/DashboardWindow.xaml`:
```xml
<Window x:Class="KlstBackup.Views.DashboardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:KlstBackup.ViewModels"
        Title="Active Backup Tasks"
        Width="600" Height="400"
        MinWidth="500" MinHeight="300"
        WindowStartupLocation="CenterScreen"
        Topmost="True">

    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ListBox Grid.Row="0" ItemsSource="{Binding ActiveTasks}"
                 HorizontalContentAlignment="Stretch">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Border BorderBrush="#DDD" BorderThickness="1" CornerRadius="4"
                            Padding="10" Margin="0,0,0,8" Background="#FAFAFA">
                        <StackPanel>
                            <DockPanel>
                                <TextBlock Text="{Binding JobName}" FontWeight="Bold" FontSize="14" />
                                <TextBlock Text="{Binding Priority}" HorizontalAlignment="Right"
                                           Foreground="Gray" FontSize="11" />
                            </DockPanel>

                            <TextBlock Text="{Binding Status}" Margin="0,4,0,0" FontSize="12" />

                            <ProgressBar Height="16" Margin="0,8,0,0"
                                         Minimum="0" Maximum="100"
                                         Value="{Binding ProgressPercent}" />

                            <TextBlock Text="{Binding ProgressText}" Margin="0,4,0,0"
                                       FontSize="11" Foreground="Gray" />

                            <TextBlock Text="{Binding ElapsedTime}" Margin="0,2,0,0"
                                       FontSize="11" Foreground="Gray" />

                            <TextBlock Text="{Binding CurrentFile}" Margin="0,2,0,0"
                                       FontSize="10" Foreground="Gray"
                                       TextTrimming="CharacterEllipsis" />

                            <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                <Button Content="Pause" Command="{Binding DataContext.PauseTaskCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                        CommandParameter="{Binding}" Padding="10,4" Margin="0,0,6,0"
                                        Visibility="{Binding IsPaused, Converter={StaticResource InverseBoolToVis}}" />
                                <Button Content="Resume" Command="{Binding DataContext.ResumeTaskCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                        CommandParameter="{Binding}" Padding="10,4" Margin="0,0,6,0"
                                        Visibility="{Binding IsPaused, Converter={StaticResource BoolToVis}}" />
                                <Button Content="Cancel" Command="{Binding DataContext.CancelTaskCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                        CommandParameter="{Binding}" Padding="10,4" />
                            </StackPanel>
                        </StackPanel>
                    </Border>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right">
            <CheckBox Content="Show Resource Monitor" IsChecked="{Binding ShowResourceMonitor}"
                      VerticalAlignment="Center" Margin="0,0,10,0" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Create DashboardWindow code-behind**

Create `src/klst-backup/Views/DashboardWindow.xaml.cs`:
```csharp
using System.Windows;
using KlstBackup.Services;
using KlstBackup.ViewModels;

namespace KlstBackup.Views;

public partial class DashboardWindow : Window
{
    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/klst-backup/Views/DashboardWindow.xaml src/klst-backup/Views/DashboardWindow.xaml.cs
git commit -m "feat: add DashboardWindow UI for real-time task monitoring"
```

---

### Task 10: Integration with Existing Services

**Files:**
- Modify: `src/klst-backup/Services/BackupEngine.cs`
- Modify: `src/klst-backup/Services/SchedulerService.cs`
- Modify: `src/klst-backup/ViewModels/MainViewModel.cs`
- Modify: `src/klst-backup/App.xaml.cs`

- [ ] **Step 1: Update BackupEngine to support pause/resume**

Modify `src/klst-backup/Services/BackupEngine.cs` to accept `BackupTask` parameter and check `IsPaused` flag in file copy loops.

- [ ] **Step 2: Update SchedulerService to use TaskQueueManager**

Modify `src/klst-backup/Services/SchedulerService.cs` to integrate with TaskQueueManager for concurrent task management.

- [ ] **Step 3: Update MainViewModel to launch dashboard**

Modify `src/klst-backup/ViewModels/MainViewModel.cs` to add command for opening DashboardWindow.

- [ ] **Step 4: Update App.xaml.cs to initialize new services**

Modify `src/klst-backup/App.xaml.cs` to initialize TaskQueueManager, CheckpointStore, and ResourceMonitor.

- [ ] **Step 5: Run all tests**

Run: `dotnet test tests/klst-backup.Tests`
Expected: All tests pass

- [ ] **Step 6: Build and run application**

Run: `dotnet build src/klst-backup/klst-backup.csproj`
Run: `dotnet run --project src/klst-backup`
Expected: Application starts, dashboard window opens, backups can run concurrently

- [ ] **Step 7: Commit**

```bash
git add src/klst-backup/Services/BackupEngine.cs src/klst-backup/Services/SchedulerService.cs src/klst-backup/ViewModels/MainViewModel.cs src/klst-backup/App.xaml.cs
git commit -m "feat: integrate TaskQueueManager with existing backup services"
```

---

## Summary

This plan implements a complete background task management system with:
- ✅ Multiple concurrent backups (one per physical disk)
- ✅ Pause/resume between files
- ✅ Persistent checkpoints (SQLite + JSON)
- ✅ Floating dashboard window with real-time updates
- ✅ Priority-based task scheduling
- ✅ Resource monitoring (optional)
- ✅ Comprehensive test coverage

**Total Tasks:** 10  
**Estimated Time:** 4-6 hours  
**Test Coverage:** Unit tests for all core components
