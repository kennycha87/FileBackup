# Background Task Management System for File Backup Application

**Date:** 2026-09-12  
**Status:** Draft  
**Version:** 1.0

## Executive Summary

This specification defines a background task management system for long-running backup operations in the File Backup application. The system enables multiple concurrent backup tasks with pause/resume capabilities, persistent checkpoints, and a dedicated floating dashboard window for real-time monitoring.

## Problem Statement

The current File Backup application has the following limitations:
- Only one backup can run at a time (single `IsBusy` flag)
- No pause/resume functionality (only cancel)
- No persistent checkpoints for interrupted backups
- Progress displayed only in status bar (limited visibility)
- No resource-aware concurrency control

## Solution Overview

Implement a Task Queue Architecture with:
- Multiple concurrent backup tasks (one per physical disk)
- Pause/resume between files with persistent checkpoints
- Separate floating dashboard window with real-time updates
- Smart concurrency control based on physical disk allocation
- Hybrid storage (SQLite + JSON) for job metadata and checkpoint state

## Architectural Decisions

### 1. Task Queue Architecture
**Decision:** Use Task Queue Architecture for sequential processing of backup jobs  
**Rationale:** Simpler to implement, easier to understand, good for sequential processing  
**Rejected Alternatives:**
- Event-Driven Architecture: More complex, harder to debug
- Actor Model: Overkill for this use case

### 2. Hybrid Storage Model
**Decision:** SQLite for structured job metadata, JSON for flexible checkpoint state  
**Rationale:** Best of both worlds - structured queries for metadata, flexible schema for checkpoints  
**Rejected Alternatives:**
- SQLite only: Less flexible for checkpoint state
- JSON only: Poor query performance for metadata

### 3. Concurrency Control
**Decision:** One backup per physical disk  
**Rationale:** Optimizes I/O performance, prevents disk contention  
**Rejected Alternatives:**
- No limit: Risk of I/O bottleneck
- Fixed limit: Doesn't adapt to hardware

### 4. Pause/Resume Granularity
**Decision:** Pause between files (not mid-file)  
**Rationale:** Safer, no partial file copies, simpler implementation  
**Rejected Alternatives:**
- Immediate pause: Requires handling partial files, more complex

### 5. Error Handling Policy
**Decision:** Skip-and-continue with error logging  
**Rationale:** Maximizes backup completion, logs errors for review  
**Rejected Alternatives:**
- Stop immediately: Poor user experience for minor errors
- Retry only: May hang on persistent failures

### 6. Resumption Safety Policy
**Decision:** Warn-and-ask when paths change between pause and resume  
**Rationale:** Prevents accidental data loss, gives user control  
**Rejected Alternatives:**
- Auto-invalidate: May discard valid checkpoints
- Auto-adapt: Complex, risk of silent failures

## Functional Requirements

### FR-1: Active Backup Task Dashboard
**Description:** Separate floating window displaying all running backup tasks in real-time

**Requirements:**
- FR-1.1: Display all currently running backup jobs
- FR-1.2: Show start time and elapsed duration for each task
- FR-1.3: Show current progress (files processed vs. total files)
- FR-1.4: Show success/failed item counts
- FR-1.5: Show current status (Processing, Paused, Completed, Failed)
- FR-1.6: Provide control buttons: Pause, Resume, Cancel
- FR-1.7: Auto-refresh every few seconds
- FR-1.8: Optional resource monitoring toggle (CPU%, memory, disk I/O)
- FR-1.9: Support light/dark mode
- FR-1.10: Display priority level (High/Normal/Low)

### FR-2: Pause and Resume Functionality
**Description:** Enable users to pause and resume active backup tasks

**Requirements:**
- FR-2.1: Pause backup between file copies (not mid-file)
- FR-2.2: Save checkpoint state to disk (JSON file)
- FR-2.3: Persist checkpoint across app restarts
- FR-2.4: Display visual feedback when task is paused
- FR-2.5: Show "Resume" button when task is paused
- FR-2.6: Validate checkpoint before resuming
- FR-2.7: Warn user if source/destination paths changed
- FR-2.8: Allow user to choose: resume from checkpoint OR start fresh

### FR-3: Resumption Prompt on Re-execution
**Description:** Detect incomplete backups and offer resumption options

**Requirements:**
- FR-3.1: Detect incomplete/abandoned backup jobs on startup
- FR-3.2: Show dialog listing all incomplete backups
- FR-3.3: Display checkpoint details (creation time, files copied, estimated time)
- FR-3.4: Allow user to select which backups to resume
- FR-3.5: Validate checkpoint integrity before resuming
- FR-3.6: Show comparison: estimated time to resume vs. start fresh

### FR-4: Multiple Concurrent Backups
**Description:** Support running multiple backup tasks simultaneously

**Requirements:**
- FR-4.1: Allow multiple backup jobs to run concurrently
- FR-4.2: Limit concurrency to one backup per physical disk
- FR-4.3: Queue additional backups when limit reached
- FR-4.4: Support priority levels (High/Normal/Low)
- FR-4.5: Higher priority backups get resources first
- FR-4.6: Display all running tasks in dashboard

### FR-5: Checkpoint Persistence
**Description:** Store backup checkpoints that survive app restarts

**Requirements:**
- FR-5.1: Store checkpoint state in JSON files
- FR-5.2: Store job metadata in SQLite database
- FR-5.3: Include file list, current position, and progress in checkpoint
- FR-5.4: Validate checkpoint integrity on load
- FR-5.5: Automatically cleanup old checkpoints based on retention policy
- FR-5.6: Support checkpoint migration if schema changes

### FR-6: Progress Tracking and Notifications
**Description:** Provide real-time feedback on backup progress

**Requirements:**
- FR-6.1: Update dashboard with real-time progress
- FR-6.2: Update main window status bar
- FR-6.3: Show toast notification on completion/failure
- FR-6.4: Display files copied, bytes copied, and percentage complete
- FR-6.5: Show estimated time remaining
- FR-6.6: Log all progress events for debugging

### FR-7: Error Handling and Recovery
**Description:** Handle errors gracefully without aborting entire backup

**Requirements:**
- FR-7.1: Skip problematic files and continue backup
- FR-7.2: Log errors with details (file path, error message, timestamp)
- FR-7.3: Display warnings in dashboard for skipped files
- FR-7.4: Retry transient failures with exponential backoff
- FR-7.5: Notify user of critical failures
- FR-7.6: Update checkpoint after each successful file copy

### FR-8: Scheduling Enhancements
**Description:** Enhanced scheduling with priority and concurrency awareness

**Requirements:**
- FR-8.1: Support basic scheduling (daily, weekly, monthly)
- FR-8.2: Support advanced scheduling (custom intervals, multiple times per day)
- FR-8.3: Respect priority levels when scheduling
- FR-8.4: Queue scheduled backups if concurrency limit reached
- FR-8.5: Allow manual backups to override scheduled backups (with user confirmation)

## Non-Functional Requirements

### NFR-1: Performance
- Backup operations run on background threads (no UI blocking)
- Efficient file scanning with timestamp + checksum verification
- Minimal resource usage when idle
- Support for advanced optimization (multi-threaded copying, file deduplication)

### NFR-2: Reliability
- Checkpoints must be atomically written (no partial writes)
- Checkpoint validation before resumption
- Graceful degradation on errors
- No data corruption on pause/resume

### NFR-3: Usability
- Dashboard window is easy to understand at a glance
- Clear visual distinction between running, paused, and completed tasks
- Intuitive controls (Pause, Resume, Cancel)
- Helpful error messages and warnings

### NFR-4: Maintainability
- Clear separation of concerns (TaskQueueManager, BackupTask, CheckpointStore)
- Comprehensive logging for debugging
- Unit tests for core components
- Documentation for public APIs

### NFR-5: Security
- Rely on OS-level file permissions
- No additional encryption or secure deletion
- Preserve NTFS ACLs and ownership information

## Technical Design

### Component Architecture

```
┌─────────────────────────────────────────────────────────────
│                      UI Layer                                │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ──────────────────────────────────┐ │
│  │  MainWindow      │  │  DashboardWindow (Floating)      │ │
│  │  - Status Bar    │  │  - Task List                     │ │
│  │  - Progress      │  │  - Progress Bars                 │ │
│  │  - Controls      │  │  - Resource Monitor (optional)   │ │
│  ──────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   ViewModel Layer                            │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌────────────────────────────────── │
│  │ MainViewModel    │  │ DashboardViewModel               │ │
│  │ - Jobs           │  │ - ActiveTasks                    │ │
│  │ - History        │  │ - ResourceUsage                  │ │
│  │ - Settings       │  │ - Controls                       │ │
│  └──────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Service Layer                              │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ TaskQueueManager │  │ SchedulerService (Enhanced)      │ │
│  │ - Task Queue     │  │ - Priority Scheduling            │ │
│  │ - Concurrency    │  │ - Trigger Management             │ │
│  │ - Priority       │  │ - Job Due Calculation            │ │
│  └──────────────────┘  └──────────────────────────────────┘ │
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ BackupEngine     │  │ CheckpointStore                  │ │
│  │ - File Copy      │  │ - SQLite (metadata)              │ │
│  │ - Pause/Resume   │  │ - JSON (checkpoint state)        │ │
│  │ - Checksum       │  │ - Validation                     │ │
│  └──────────────────┘  └──────────────────────────────────┘ │
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ NotificationSvc  │  │ ResourceMonitor                  │ │
│  │ - Toast          │  │ - CPU Usage                      │ │
│  │ - Status Bar     │  │ - Memory Usage                   │ │
│  │ - Logging        │  │ - Disk I/O                       │ │
│  └──────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Data Layer                                 │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ SQLite Database  │  │ JSON Files                       │ │
│  │ - Jobs           │  │ - Checkpoint State               │ │
│  │ - History        │  │ - File Lists                     │ │
│  │ - Settings       │  │ - Progress                       │ │
│  │ - Retention      │  │ - Metadata                       │ │
│  ──────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Key Classes

#### TaskQueueManager
**Responsibility:** Central coordinator for backup tasks

**Methods:**
- `EnqueueTask(BackupTask task)` - Add task to queue
- `DequeueTask()` - Get next task based on priority
- `StartTask(BackupTask task)` - Start task execution
- `PauseTask(Guid taskId)` - Pause running task
- `ResumeTask(Guid taskId)` - Resume paused task
- `CancelTask(Guid taskId)` - Cancel task
- `GetActiveTasks()` - Get all active tasks
- `GetDiskAllocation()` - Get disk-to-task mapping

**Properties:**
- `MaxConcurrentTasks` - Maximum concurrent tasks (auto-calculated)
- `ActiveTasks` - Currently running tasks
- `QueuedTasks` - Tasks waiting to run

#### BackupTask
**Responsibility:** Individual backup operation with pause/resume

**Methods:**
- `Start()` - Begin backup execution
- `Pause()` - Pause between file copies
- `Resume()` - Resume from checkpoint
- `Cancel()` - Cancel backup
- `GetProgress()` - Get current progress
- `SaveCheckpoint()` - Save checkpoint to disk
- `LoadCheckpoint()` - Load checkpoint from disk

**Properties:**
- `TaskId` - Unique identifier
- `JobId` - Associated backup job
- `Status` - Current status (Running, Paused, Completed, Failed)
- `Priority` - Task priority (High, Normal, Low)
- `StartTime` - When task started
- `ElapsedTime` - Time elapsed
- `FilesProcessed` - Number of files processed
- `TotalFiles` - Total files to process
- `BytesProcessed` - Bytes processed
- `TotalBytes` - Total bytes to process
- `CurrentFile` - Currently processing file
- `Checkpoint` - Current checkpoint state

#### CheckpointStore
**Responsibility:** Hybrid storage for job metadata and checkpoint state

**Methods:**
- `SaveCheckpoint(Guid taskId, CheckpointState state)` - Save checkpoint
- `LoadCheckpoint(Guid taskId)` - Load checkpoint
- `DeleteCheckpoint(Guid taskId)` - Delete checkpoint
- `ListCheckpoints()` - List all checkpoints
- `ValidateCheckpoint(Guid taskId)` - Validate checkpoint integrity
- `CleanupOldCheckpoints(RetentionPolicy policy)` - Cleanup old checkpoints
- `SaveJobMetadata(JobMetadata metadata)` - Save job metadata
- `LoadJobMetadata(Guid jobId)` - Load job metadata
- `ListJobMetadata()` - List all job metadata

**Storage:**
- SQLite: Job metadata, history, settings, retention policies
- JSON: Checkpoint state (file list, current position, progress)

#### DashboardWindow
**Responsibility:** Floating window for real-time task monitoring

**Features:**
- Auto-refresh every few seconds
- Display all active tasks with progress
- Control buttons (Pause, Resume, Cancel)
- Optional resource monitoring
- Light/dark mode support
- Always on top option

#### ResourceMonitor
**Responsibility:** Optional system resource tracking

**Methods:**
- `GetCpuUsage()` - Get CPU usage percentage
- `GetMemoryUsage()` - Get memory usage
- `GetDiskIO(Guid taskId)` - Get disk I/O for specific task
- `StartMonitoring()` - Start monitoring
- `StopMonitoring()` - Stop monitoring

### Data Flow

#### Starting a Backup Task
1. User clicks "Run Now" or scheduler triggers backup
2. `MainViewModel` creates `BackupTask` and calls `TaskQueueManager.EnqueueTask()`
3. `TaskQueueManager` checks concurrency limit (one per physical disk)
4. If limit not reached, task starts immediately; otherwise, queued
5. `BackupTask.Start()` begins file scanning
6. Checkpoint created with file list and initial state
7. File copying begins with progress updates
8. Dashboard displays real-time progress

#### Pausing a Backup Task
1. User clicks "Pause" button in dashboard
2. `DashboardViewModel` calls `TaskQueueManager.PauseTask(taskId)`
3. `BackupTask.Pause()` sets pause flag
4. Current file copy completes (no mid-file pause)
5. `BackupTask.SaveCheckpoint()` saves state to JSON
6. Task status changes to "Paused"
7. Dashboard updates to show paused state

#### Resuming a Backup Task
1. User clicks "Resume" button in dashboard
2. `DashboardViewModel` calls `TaskQueueManager.ResumeTask(taskId)`
3. `BackupTask.LoadCheckpoint()` loads checkpoint from JSON
4. Checkpoint validated (file list, position, integrity)
5. If paths changed, show warn-and-ask dialog
6. User chooses: resume OR start fresh
7. If resume, `BackupTask.Resume()` continues from saved position
8. Dashboard updates to show running state

#### App Startup with Incomplete Backups
1. App starts, `CheckpointStore.ListCheckpoints()` loads all checkpoints
2. Filter for incomplete/paused checkpoints
3. Show startup dialog listing incomplete backups
4. Display checkpoint details (creation time, files copied, estimated time)
5. User selects which backups to resume
6. Selected backups enqueued in `TaskQueueManager`
7. Dashboard opens automatically if tasks are running

### Storage Structure

#### SQLite Database (`%APPDATA%\FileBackup\backup.db`)
**Tables:**
- `Jobs` - Job metadata (id, name, source, destination, schedule, priority, enabled)
- `History` - Run history (id, job_id, started_utc, type, status, files_copied, bytes_copied, duration, message)
- `Settings` - Application settings (key, value)
- `RetentionPolicies` - Retention rules (id, job_id, max_backups, max_days, enabled)

#### JSON Files (`%APPDATA%\FileBackup\checkpoints\{taskId}.json`)
**Structure:**
```json
{
  "taskId": "guid",
  "jobId": "guid",
  "status": "Paused|Completed|Failed",
  "createdAt": "2026-09-12T10:00:00Z",
  "updatedAt": "2026-09-12T10:30:00Z",
  "sourcePath": "C:\\Source",
  "destPath": "D:\\Backup",
  "backupType": "Full|Differential",
  "files": [
    {
      "relativePath": "folder/file.txt",
      "size": 1024,
      "lastWriteTimeUtc": "2026-09-12T09:00:00Z",
      "checksum": "sha256hash",
      "copied": true
    }
  ],
  "currentIndex": 150,
  "filesProcessed": 150,
  "totalFiles": 500,
  "bytesProcessed": 153600,
  "totalBytes": 512000,
  "errors": [
    {
      "filePath": "folder/locked.txt",
      "error": "Access denied",
      "timestamp": "2026-09-12T10:15:00Z"
    }
  ]
}
```

### Concurrency Control Algorithm

```
Function GetMaxConcurrentTasks():
    physicalDisks = GetPhysicalDiskCount()
    return physicalDisks  // One backup per physical disk

Function CanStartTask(BackupTask task):
    activeTasks = GetActiveTasks()
    taskDisk = GetPhysicalDisk(task.Job.DestPath)
    
    // Check if disk already has active backup
    foreach activeTask in activeTasks:
        activeDisk = GetPhysicalDisk(activeTask.Job.DestPath)
        if activeDisk == taskDisk:
            return false
    
    return true

Function EnqueueTask(BackupTask task):
    if CanStartTask(task):
        StartTask(task)
    else:
        AddToQueue(task)  // Queue by priority
```

### Checkpoint Validation

```
Function ValidateCheckpoint(CheckpointState checkpoint):
    // Check file exists
    if not FileExists(checkpoint.Path):
        return Invalid("Checkpoint file missing")
    
    // Check JSON is valid
    try:
        data = ParseJSON(checkpoint.Path)
    catch:
        return Invalid("Checkpoint file corrupted")
    
    // Check required fields
    if not data.HasField("taskId"):
        return Invalid("Missing taskId")
    if not data.HasField("files"):
        return Invalid("Missing file list")
    if not data.HasField("currentIndex"):
        return Invalid("Missing current position")
    
    // Check source/destination still exist
    if not DirectoryExists(data.sourcePath):
        return Invalid("Source path no longer exists")
    if not DirectoryExists(data.destPath):
        return Invalid("Destination path no longer exists")
    
    // Check file list integrity
    foreach file in data.files:
        if not file.HasField("relativePath"):
            return Invalid("File entry missing relativePath")
        if not file.HasField("size"):
            return Invalid("File entry missing size")
    
    return Valid
```

## User Interface Design

### Dashboard Window Layout

```
┌─────────────────────────────────────────────────────────────┐
│  Active Backup Tasks                           [─] [□] [] │
├─────────────────────────────────────────────────────────────┤
│  ───────────────────────────────────────────────────────┐ │
│  │ 🔵 Job: Documents Backup                    Priority: High │
│  │ Status: Running | Progress: 150/500 files (30%)      │ │
│  │ Started: 10:00 AM | Elapsed: 00:30:00                │ │
│  │ Current: folder/document.pdf                         │ │
│  │ Speed: 5.2 MB/s | ETA: 00:01:10                      │ │
│  │ [ Pause] [✕ Cancel]                                 │ │
│  └───────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Job: Photos Backup                       Priority: Normal │
│  │ Status: Paused | Progress: 75/200 files (37.5%)      │ │
│  │ Started: 9:45 AM | Paused: 10:15 AM                  │ │
│  │ Last File: photos/vacation.jpg                       │ │
│  │ [▶ Resume] [ Cancel]                                │ │
│  └───────────────────────────────────────────────────────┘ │
│                                                             │
│  [ Show Resource Monitor]                                 │
│  ┌─────────────────────────────────────────────────────── │
│  │ CPU: 45% | Memory: 512 MB | Disk: 120 MB/s           │ │
│  └───────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Startup Dialog (Incomplete Backups)

```
┌─────────────────────────────────────────────────────────────┐
│  Incomplete Backups Found                                    │
├─────────────────────────────────────────────────────────────┤
│  The following backups were interrupted:                     │
│                                                             │
│  ☑ Documents Backup                                          │
│    - Paused: 2 hours ago                                    │
│    - Progress: 150/500 files (30%)                          │
│    - Estimated time to resume: 1 minute                     │
│    - Estimated time to start fresh: 3 minutes               │
│                                                             │
│  ☑ Photos Backup                                             │
│    - Paused: 1 day ago                                      │
│    - Progress: 75/200 files (37.5%)                         │
│    - Estimated time to resume: 30 seconds                   │
│    - Estimated time to start fresh: 1 minute                │
│                                                             │
│  [Resume Selected]  [Start All Fresh]  [Cancel All]        │
└─────────────────────────────────────────────────────────────┘
```

### Path Change Warning Dialog

```
┌─────────────────────────────────────────────────────────────┐
│  Source/Destination Changed                                  │
├─────────────────────────────────────────────────────────────┤
│  The source or destination path has changed since this       │
│  backup was paused:                                          │
│                                                             │
│  Original Source: C:\OldSource                              │
│  Current Source:  C:\NewSource                              │
│                                                             │
│  Original Destination: D:\OldBackup                         │
│  Current Destination: E:\NewBackup                          │
│                                                             │
│  Checkpoint Details:                                         │
│  - Created: 2 hours ago                                     │
│  - Files copied: 150/500 (30%)                              │
│  - Estimated time to resume: 1 minute                       │
│  - Estimated time to start fresh: 3 minutes                 │
│                                                             │
│  ⚠ Resuming with different paths may cause issues.          │
│                                                             │
│  [Resume Anyway]  [Start Fresh]  [Cancel]                   │
└─────────────────────────────────────────────────────────────┘
```

## Testing Strategy

### Unit Tests
- TaskQueueManager: Queue operations, priority ordering, concurrency control
- BackupTask: Pause/resume logic, checkpoint save/load, progress tracking
- CheckpointStore: CRUD operations, validation, cleanup
- ResourceMonitor: CPU/memory/disk I/O measurement

### Integration Tests
- Multiple concurrent backups with disk allocation
- Pause/resume across app restarts
- Checkpoint validation and recovery
- Priority-based task scheduling

### UI Tests
- Dashboard auto-refresh
- Control button functionality
- Resource monitor toggle
- Light/dark mode switching

### Performance Tests
- Concurrent backup throughput
- Checkpoint save/load performance
- Resource monitoring overhead
- Large file list handling

## Migration Plan

### Phase 1: Core Infrastructure (Week 1-2)
- Implement TaskQueueManager
- Implement CheckpointStore (SQLite + JSON)
- Implement BackupTask with pause/resume
- Unit tests for core components

### Phase 2: Dashboard UI (Week 3)
- Create DashboardWindow
- Implement DashboardViewModel
- Auto-refresh mechanism
- Control buttons (Pause, Resume, Cancel)

### Phase 3: Integration (Week 4)
- Integrate with existing SchedulerService
- Update MainViewModel for concurrency
- Startup dialog for incomplete backups
- Path change warning dialog

### Phase 4: Enhanced Features (Week 5)
- Resource monitoring
- Priority-based scheduling
- Advanced scheduling options
- Toast notifications

### Phase 5: Polish and Testing (Week 6)
- Light/dark mode
- Localization updates
- Performance optimization
- Comprehensive testing

## Risks and Mitigations

### Risk 1: Checkpoint Corruption
**Impact:** High - Data loss, unable to resume  
**Mitigation:** Atomic writes, validation on load, backup checkpoints

### Risk 2: Performance Degradation
**Impact:** Medium - Slow backups, high resource usage  
**Mitigation:** Concurrency limits, efficient file scanning, optional resource monitoring

### Risk 3: UI Freezing
**Impact:** High - Poor user experience  
**Mitigation:** Background threads, async operations, dispatcher for UI updates

### Risk 4: Disk Contention
**Impact:** Medium - Slow I/O, system slowdown  
**Mitigation:** One backup per physical disk, priority-based scheduling

### Risk 5: Checkpoint Incompatibility
**Impact:** Medium - Unable to resume after app update  
**Mitigation:** Checkpoint versioning, migration logic, backward compatibility

## Success Criteria

1. **Functional:** Users can run multiple concurrent backups with pause/resume
2. **Performance:** No UI freezing, efficient resource usage
3. **Reliability:** Checkpoints persist across app restarts, no data corruption
4. **Usability:** Dashboard is intuitive, controls are clear
5. **Maintainability:** Code is well-structured, tested, and documented

## Open Questions

1. Should we support backup templates for common scenarios?
2. Should we add basic help section with FAQs?
3. Should we include optional crash reporting?
4. Should we support advanced import/export with validation?

## References

- Current BackupEngine implementation: `src/klst-backup/Services/BackupEngine.cs`
- Current SchedulerService: `src/klst-backup/Services/SchedulerService.cs`
- Current MainViewModel: `src/klst-backup/ViewModels/MainViewModel.cs`
- Current MainWindow: `src/klst-backup/Views/MainWindow.xaml`

## Appendix

### Glossary
- **Checkpoint:** Saved state of a backup task (file list, current position, progress)
- **Task Queue:** Data structure managing backup task execution order
- **Concurrency Limit:** Maximum number of simultaneous backup tasks
- **Physical Disk:** Actual hardware disk (not partitions or virtual drives)

### Acronyms
- **ADS:** Alternate Data Streams
- **ACL:** Access Control List
- **ETA:** Estimated Time of Arrival
- **I/O:** Input/Output
- **UI:** User Interface
