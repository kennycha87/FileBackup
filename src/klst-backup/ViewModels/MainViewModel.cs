using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KlstBackup.Models;
using KlstBackup.Services;
using KlstBackup.Views;

namespace KlstBackup.ViewModels;

/// <summary>Main view model: jobs, history, restore, settings, and progress display.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly SchedulerService _scheduler;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    public ObservableCollection<JobItemViewModel> Jobs { get; } = new();

    public ObservableCollection<RunRecord> History { get; } = new();

    public ObservableCollection<string> BackupSets { get; } = new();

    [ObservableProperty]
    private JobItemViewModel? _selectedJob;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private JobItemViewModel? _restoreJob;

    [ObservableProperty]
    private string? _selectedBackupSet;

    [ObservableProperty]
    private string _restoreTargetPath = string.Empty;

    public MainViewModel()
    {
        _config = App.Config;
        _configService = App.ConfigService;
        _scheduler = App.Scheduler;
        _dispatcher = Application.Current.Dispatcher;

        foreach (var job in _config.Jobs)
        {
            Jobs.Add(new JobItemViewModel(job, SaveConfig));
        }

        StartWithWindows = StartupService.IsRegistered();

        _scheduler.JobStarting += OnSchedulerJobStarting;
        _scheduler.JobFinished += OnSchedulerJobFinished;

        if (Jobs.Count > 0)
        {
            SelectedJob = Jobs[0];
        }
    }

    private void SaveConfig()
    {
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            StatusText = "Failed to save configuration: " + ex.Message;
        }
    }

    partial void OnSelectedJobChanged(JobItemViewModel? value)
    {
        if (value is not null)
        {
            RestoreJob = value;
        }
    }

    partial void OnRestoreJobChanged(JobItemViewModel? value)
    {
        RefreshBackupSets();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        try
        {
            StartupService.SetRegistered(value);
            _config.StartWithWindows = value;
            SaveConfig();
        }
        catch (Exception ex)
        {
            StatusText = "Failed to update startup registration: " + ex.Message;
        }
    }

    private void RefreshBackupSets()
    {
        BackupSets.Clear();
        SelectedBackupSet = null;
        if (RestoreJob is null)
        {
            return;
        }

        foreach (var set in ManifestStore.ListSets(RestoreJob.Job.Id))
        {
            BackupSets.Add(set);
        }

        if (BackupSets.Count > 0)
        {
            SelectedBackupSet = BackupSets[0];
        }
    }

    private void RebuildHistory()
    {
        History.Clear();
        var all = _config.Jobs
            .SelectMany(j => j.History)
            .OrderByDescending(r => r.StartedUtc)
            .ToList();
        foreach (var record in all)
        {
            History.Add(record);
        }
    }

    // ----- Scheduler events (marshalled to the UI thread) -----

    private void OnSchedulerJobStarting(BackupJob job, bool scheduled)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (scheduled)
            {
                IsBusy = true;
                ProgressPercent = 0;
                ProgressText = string.Empty;
                StatusText = $"Scheduled backup started: {job.Name}";
            }
        });
    }

    private void OnSchedulerJobFinished(BackupJob job, BackupResult result, bool scheduled)
    {
        _dispatcher.BeginInvoke(() =>
        {
            RebuildHistory();
            RefreshBackupSets();
            var item = Jobs.FirstOrDefault(j => j.Job.Id == job.Id);
            item?.Refresh();
            if (scheduled)
            {
                IsBusy = false;
                StatusText = DescribeResult("Scheduled backup", job.Name, result);
            }
        });
    }

    private static string DescribeResult(string kind, string jobName, BackupResult result)
    {
        return result.Success
            ? $"{kind} '{jobName}' completed: {result.FilesCopied} file(s), {BackupEngine.FormatBytes(result.BytesCopied)} ({result.SetFolder})."
            : result.Cancelled
                ? $"{kind} '{jobName}' was cancelled."
                : $"{kind} '{jobName}' failed: {result.Error}";
    }

    // ----- Commands -----

    [RelayCommand]
    private void NewJob()
    {
        var dialog = new JobEditDialog(new JobEditViewModel(new BackupJob()))
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true)
        {
            var job = dialog.ViewModel.Job;
            _config.Jobs.Add(job);
            var item = new JobItemViewModel(job, SaveConfig);
            Jobs.Add(item);
            SaveConfig();
            SelectedJob = item;
            StatusText = $"Job '{job.Name}' created.";
        }
    }

    [RelayCommand]
    private void EditJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var clone = SelectedJob.Job.Clone();
        var dialog = new JobEditDialog(new JobEditViewModel(clone))
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedJob.Job.CopyEditableFrom(clone);
            SelectedJob.Refresh();
            SaveConfig();
            StatusText = $"Job '{SelectedJob.Job.Name}' updated.";
        }
    }

    [RelayCommand]
    private void DeleteJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var jobName = SelectedJob.Job.Name;
        var confirm = MessageBox.Show(
            $"Delete job '{jobName}'? Existing backup sets in the destination are kept.",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _config.Jobs.RemoveAll(j => j.Id == SelectedJob.Job.Id);
        Jobs.Remove(SelectedJob);
        SaveConfig();
        SelectedJob = Jobs.FirstOrDefault();
        StatusText = $"Job '{jobName}' deleted.";
    }

    [RelayCommand]
    private void RunNow()
    {
        if (SelectedJob is null)
        {
            StatusText = "Select a job first.";
            return;
        }

        StartRun(SelectedJob.Job);
    }

    [RelayCommand]
    private void StopRun()
    {
        _scheduler.CancelCurrent();
        StatusText = "Stopping current backup...";
    }

    private async void StartRun(BackupJob job)
    {
        if (IsBusy)
        {
            MessageBox.Show("Another backup is already running.", "File Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        StatusText = $"Backing up '{job.Name}'...";
        ProgressPercent = 0;
        ProgressText = string.Empty;
        var progress = new Progress<BackupProgress>(p =>
        {
            ProgressPercent = p.TotalFiles > 0 ? p.FilesDone * 100.0 / p.TotalFiles : 0;
            ProgressText = $"{p.FilesDone}/{p.TotalFiles} files - {BackupEngine.FormatBytes(p.BytesCopied)} - {p.CurrentFile}";
        });

        try
        {
            var result = await Task.Run(() => _scheduler.RunJob(job, scheduled: false, progress));
            StatusText = DescribeResult("Backup", job.Name, result);
            if (result.Success)
            {
                ProgressPercent = 100;
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Backup error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RebuildHistory();
            RefreshBackupSets();
        }
    }

    [RelayCommand]
    private void BrowseRestoreTarget()
    {
        var path = BrowseForFolder("Select the folder to restore into");
        if (path is not null)
        {
            RestoreTargetPath = path;
        }
    }

    [RelayCommand]
    private void Restore()
    {
        if (RestoreJob is null || SelectedBackupSet is null)
        {
            MessageBox.Show("Select a job and a backup set first.", "File Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(RestoreTargetPath))
        {
            MessageBox.Show("Choose the folder to restore into.", "File Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsBusy)
        {
            MessageBox.Show("Another operation is already running.", "File Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StartRestore(RestoreJob.Job, SelectedBackupSet, RestoreTargetPath);
    }

    private async void StartRestore(BackupJob job, string setName, string targetPath)
    {
        IsBusy = true;
        StatusText = $"Restoring '{setName}'...";
        ProgressPercent = 0;
        ProgressText = string.Empty;
        var progress = new Progress<BackupProgress>(p =>
        {
            ProgressPercent = p.TotalFiles > 0 ? p.FilesDone * 100.0 / p.TotalFiles : 0;
            ProgressText = $"{p.FilesDone}/{p.TotalFiles} files - {p.CurrentFile}";
        });

        try
        {
            var result = await Task.Run(() => App.Engine.RestoreBackup(
                job.Id, job.DestPath, setName, targetPath, progress, CancellationToken.None, null));
            StatusText = result.Success
                ? $"Restore completed: {result.FilesRestored} file(s), {BackupEngine.FormatBytes(result.BytesRestored)}."
                : result.Cancelled
                    ? "Restore was cancelled."
                    : "Restore failed: " + result.Error;
            if (result.Success)
            {
                ProgressPercent = 100;
            }
        }
        catch (Exception ex)
        {
            StatusText = "Restore error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Shows a WinForms folder browser dialog; returns the picked path or null.</summary>
    public static string? BrowseForFolder(string description)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
