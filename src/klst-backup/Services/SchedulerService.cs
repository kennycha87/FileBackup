using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KlstBackup.Models;

namespace KlstBackup.Services;

/// <summary>
/// In-app scheduler. A periodic timer checks which jobs are due and runs them
/// sequentially in the background. Backups only run while the application is
/// running (the app lives in the system tray).
/// </summary>
public class SchedulerService
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly BackupEngine _engine;
    private readonly LogService _logService;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private System.Threading.Timer? _timer;
    private int _ticking;
    private CancellationTokenSource? _currentRun;

    /// <summary>Raised when a run starts. (job, isScheduled)</summary>
    public event Action<BackupJob, bool>? JobStarting;

    /// <summary>Raised when a run finishes. (job, result, isScheduled)</summary>
    public event Action<BackupJob, BackupResult, bool>? JobFinished;

    /// <summary>Raised for non-fatal application/scheduler messages.</summary>
    public event Action<string>? AppMessage;

    public bool IsBusy => Volatile.Read(ref _ticking) == 1 && _currentRun is not null;

    public SchedulerService(AppConfig config, ConfigService configService, BackupEngine engine, LogService logService)
    {
        _config = config;
        _configService = configService;
        _engine = engine;
        _logService = logService;
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

    /// <summary>Cancels the currently running backup, if any.</summary>
    public void CancelCurrent()
    {
        try
        {
            _currentRun?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // run already finished
        }
    }

    private async void Tick(object? state)
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
                if (!await _runGate.WaitAsync(0).ConfigureAwait(false))
                {
                    break; // a manual run holds the gate; retry next tick
                }

                try
                {
                    await Task.Run(() => RunJobCore(job, scheduled: true, progress: null)).ConfigureAwait(false);
                }
                finally
                {
                    _runGate.Release();
                }
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
            return RunJobCore(job, scheduled, progress, externalToken);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private BackupResult RunJobCore(BackupJob job, bool scheduled, IProgress<BackupProgress>? progress,
        CancellationToken? externalToken = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken ?? CancellationToken.None);
        _currentRun = cts;
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

        var result = _engine.RunBackup(job, job.JobType, progress, cts.Token, log);

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

        _currentRun = null;
        JobFinished?.Invoke(job, result, scheduled);
        return result;
    }
}
