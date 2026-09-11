using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KlstBackup.Models;
using TaskStatus = KlstBackup.Models.TaskStatus;

namespace KlstBackup.Services;

/// <summary>
/// In-app scheduler. A periodic timer checks which jobs are due and enqueues them as
/// <see cref="BackupTask"/>s into the <see cref="TaskQueueManager"/>, which starts one
/// task per physical disk and runs the rest as slots free up. Manual runs go through
/// the queue too but execute inline on the caller's thread. Backups only run while the
/// application is running (the app lives in the system tray).
/// </summary>
public class SchedulerService
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly BackupEngine _engine;
    private readonly LogService _logService;
    private readonly TaskQueueManager _taskQueue;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, bool> _pumpRuns = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runs = new();
    private System.Threading.Timer? _timer;
    private int _ticking;

    /// <summary>Raised when a run starts. (job, isScheduled)</summary>
    public event Action<BackupJob, bool>? JobStarting;

    /// <summary>Raised when a run finishes. (job, result, isScheduled)</summary>
    public event Action<BackupJob, BackupResult, bool>? JobFinished;

    /// <summary>Raised for non-fatal application/scheduler messages.</summary>
    public event Action<string>? AppMessage;

    /// <summary>True while at least one backup run (manual or scheduled) is executing.</summary>
    public bool IsBusy => !_runs.IsEmpty;

    public SchedulerService(AppConfig config, ConfigService configService, BackupEngine engine, LogService logService,
        TaskQueueManager? taskQueue = null)
    {
        _config = config;
        _configService = configService;
        _engine = engine;
        _logService = logService;
        _taskQueue = taskQueue ?? new TaskQueueManager();
        _taskQueue.TaskStarted += OnQueuedTaskStarted;
    }

    public void Start()
    {
        // First tick after a short delay, then every 30 seconds.
        _timer ??= new System.Threading.Timer(Tick, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Cancels every running backup, if any.</summary>
    public void CancelCurrent()
    {
        foreach (var cts in _runs.Values)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // run already finished
            }
        }
    }

    private void Tick(object? state)
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return; // previous tick still running
        }

        try
        {
            var due = _config.Jobs.Where(j => SchedulerMath.IsDue(j, DateTime.Now)).ToList();
            foreach (var job in due)
            {
                // Mark the slot as consumed BEFORE enqueueing: a run that is waiting in the
                // queue for a free disk slot must not re-trigger on the next tick.
                job.LastRunUtc = DateTime.UtcNow;
                try
                {
                    _configService.Save(_config);
                }
                catch (Exception ex)
                {
                    AppMessage?.Invoke("Failed to save configuration: " + ex.Message);
                }

                var task = new BackupTask { JobId = job.Id, Job = job };
                _pumpRuns[task.TaskId] = true;
                _taskQueue.EnqueueTask(task);
                // If a disk slot was free the task started right away and OnQueuedTaskStarted
                // is already running it; otherwise the pump starts it when a slot frees up.
            }
        }
        catch (Exception ex)
        {
            AppMessage?.Invoke("Scheduler error: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _ticking, 0);
        }
    }

    /// <summary>
    /// Executes scheduler-owned tasks (scheduled runs) as the queue starts them. Tasks
    /// enqueued by <see cref="RunJob"/> are not registered here: they execute inline on
    /// their caller's thread so the whole persisted write path stays on that thread.
    /// </summary>
    private void OnQueuedTaskStarted(BackupTask task)
    {
        if (!_pumpRuns.TryRemove(task.TaskId, out _))
        {
            return; // caller-driven (manual) task
        }

        _ = Task.Run(() =>
        {
            try
            {
                RunJobCore(task.Job, scheduled: true, progress: null, task: task);
            }
            catch (Exception ex)
            {
                _taskQueue.FailTask(task.TaskId); // best effort: free the disk slot
                AppMessage?.Invoke("Scheduler error: " + ex.Message);
            }
        });
    }

    /// <summary>
    /// Runs a job synchronously. Intended to be called via Task.Run from the UI.
    /// Throws InvalidOperationException when another backup is already running.
    /// </summary>
    public BackupResult RunJob(BackupJob job, bool scheduled, IProgress<BackupProgress>? progress,
        CancellationToken? externalToken = null)
    {
        if (!_runGate.Wait(0))
        {
            throw new InvalidOperationException("Another backup is already running.");
        }

        try
        {
            var task = new BackupTask { JobId = job.Id, Job = job };
            _taskQueue.EnqueueTask(task);

            if (task.Status == TaskStatus.Queued)
            {
                // Every disk slot is busy (e.g. a scheduled run owns this disk). Back the task
                // out of the queue and keep the manual run's immediate-or-busy contract
                // instead of silently parking it.
                if (_taskQueue.TryRemoveQueued(task.TaskId))
                {
                    throw new InvalidOperationException("Another backup is already running.");
                }

                // else: the task started between the status read and the removal - run it below.
            }

            return RunJobCore(job, scheduled, progress, externalToken, task);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private BackupResult RunJobCore(BackupJob job, bool scheduled, IProgress<BackupProgress>? progress,
        CancellationToken? externalToken = null, BackupTask? task = null)
    {
        task ??= new BackupTask { JobId = job.Id, Job = job };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken ?? CancellationToken.None);
        _runs[task.TaskId] = cts;
        var startedUtc = DateTime.UtcNow;
        JobStarting?.Invoke(job, scheduled);

        using var log = _logService.BeginRun(job.Name);
        log.Info($"Starting {(scheduled ? "scheduled" : "manual")} backup ({job.JobType}) for '{job.Name}'.");

        if (scheduled)
        {
            // Mark the slot as consumed before running so a slow run does not re-trigger.
            job.LastRunUtc = startedUtc;
            try
            {
                _configService.Save(_config);
            }
            catch (Exception ex)
            {
                log.Warning("Could not persist LastRun time: " + ex.Message);
            }
        }

        BackupResult result;
        try
        {
            result = _engine.RunBackup(job, job.JobType, progress, cts.Token, log, task);
        }
        finally
        {
            _runs.TryRemove(task.TaskId, out _);
        }

        var status = result.Cancelled ? RunStatus.Cancelled
            : result.Success ? (result.Warnings > 0 ? RunStatus.CompletedWithWarnings : RunStatus.Completed)
            : RunStatus.Failed;
        job.History.Add(new RunRecord
        {
            JobName = job.Name,
            StartedUtc = startedUtc,
            Type = result.Type,
            Status = status,
            FilesCopied = result.FilesCopied,
            BytesCopied = result.BytesCopied,
            Duration = result.Duration,
            Message = result.Success ? result.SetFolder ?? string.Empty
                : result.Cancelled ? "Cancelled"
                : result.Error ?? "Failed"
        });
        if (job.History.Count > 200)
        {
            job.History.RemoveRange(0, job.History.Count - 200);
        }

        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            AppMessage?.Invoke("Failed to save configuration: " + ex.Message);
        }

        // Finalize the task in the queue so its disk slot frees up and queued work can start.
        if (result.Cancelled)
        {
            _taskQueue.CancelTask(task.TaskId);
        }
        else if (result.Success)
        {
            _taskQueue.CompleteTask(task.TaskId);
        }
        else
        {
            _taskQueue.FailTask(task.TaskId);
        }

        JobFinished?.Invoke(job, result, scheduled);
        return result;
    }
}
