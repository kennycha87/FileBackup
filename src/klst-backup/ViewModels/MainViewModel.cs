using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KlstBackup.Models;
using KlstBackup.Resources;
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
    private string _statusText = Strings.Status_Ready;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string? _language;

    [ObservableProperty]
    private JobItemViewModel? _restoreJob;

    [ObservableProperty]
    private string? _selectedBackupSet;

    [ObservableProperty]
    private string _restoreTargetPath = string.Empty;

    /// <summary>A row in the Settings language picker. <see cref="NativeName"/> is deliberately NOT
    /// localized so a user can always find their own language regardless of the current UI language.</summary>
    public sealed record LanguageOption(string? Tag, string NativeName);

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[]
    {
        new LanguageOption(null, "Follow system / 跟隨系統"),
        new LanguageOption("en-US", "English (United States)"),
        new LanguageOption("zh-HK", "繁體中文（香港）"),
        new LanguageOption("zh-CN", "简体中文（中国）"),
    };

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
        _language = _config.Language;

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
            StatusText = string.Format(Strings.Status_ConfigSaveFailed, ex.Message);
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
            StatusText = string.Format(Strings.Status_StartupFailed, ex.Message);
        }
    }

    partial void OnLanguageChanged(string? value)
    {
        _config.Language = value;
        SaveConfig();
        StatusText = Strings.Status_LanguageRestartHint;
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
                StatusText = string.Format(Strings.Status_ScheduledStarted, job.Name);
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
                StatusText = DescribeResult(Strings.Kind_Scheduled, job.Name, result);
            }
        });
    }

    private static string DescribeResult(string kind, string jobName, BackupResult result)
    {
        return result.Success
            ? string.Format(Strings.Status_BackupCompleted, kind, jobName, result.FilesCopied,
                            BackupEngine.FormatBytes(result.BytesCopied), result.SetFolder)
            : result.Cancelled
                ? string.Format(Strings.Status_BackupCancelled, kind, jobName)
                : string.Format(Strings.Status_BackupFailed, kind, jobName, result.Error);
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
            StatusText = string.Format(Strings.Status_JobCreated, job.Name);
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
            StatusText = string.Format(Strings.Status_JobUpdated, SelectedJob.Job.Name);
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
            string.Format(Strings.Msg_DeleteConfirm, jobName),
            Strings.Msg_DeleteCaption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _config.Jobs.RemoveAll(j => j.Id == SelectedJob.Job.Id);
        Jobs.Remove(SelectedJob);
        SaveConfig();
        SelectedJob = Jobs.FirstOrDefault();
        StatusText = string.Format(Strings.Status_JobDeleted, jobName);
    }

    [RelayCommand]
    private void RunNow()
    {
        if (SelectedJob is null)
        {
            StatusText = Strings.Status_SelectJobFirst;
            return;
        }

        StartRun(SelectedJob.Job);
    }

    [RelayCommand]
    private void StopRun()
    {
        _scheduler.CancelCurrent();
        StatusText = Strings.Status_Stopping;
    }

    private DashboardWindow? _dashboard;

    /// <summary>Shows the floating task dashboard, or brings the existing one to the front.</summary>
    [RelayCommand]
    private void OpenDashboard()
    {
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow(new DashboardViewModel(App.TaskQueue, App.ResourceMonitor));
            _dashboard.Closed += (_, _) => _dashboard = null;
            _dashboard.Show();
        }
        else
        {
            _dashboard.Activate();
        }
    }

    private async void StartRun(BackupJob job)
    {
        if (IsBusy)
        {
            MessageBox.Show(Strings.Msg_Busy, Strings.Msg_Caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        StatusText = string.Format(Strings.Status_BackingUp, job.Name);
        ProgressPercent = 0;
        ProgressText = string.Empty;
        // Hoisted out of the per-file handler: ResourceManager.GetString runs once per run, never once
        // per copied file. No resource lookup and no CultureInfo allocation inside the lambda.
        var progressTemplate = Strings.Status_ProgressFiles;
        var progress = new Progress<BackupProgress>(p =>
        {
            ProgressPercent = p.TotalFiles > 0 ? p.FilesDone * 100.0 / p.TotalFiles : 0;
            ProgressText = string.Format(progressTemplate, p.FilesDone, p.TotalFiles,
                                         BackupEngine.FormatBytes(p.BytesCopied), p.CurrentFile);
        });

        try
        {
            var result = await Task.Run(() => _scheduler.RunJob(job, scheduled: false, progress));
            StatusText = DescribeResult(Strings.Kind_Manual, job.Name, result);
            if (result.Success)
            {
                ProgressPercent = 100;
            }
        }
        catch (InvalidOperationException)
        {
            // The only InvalidOperationException reachable here is the "another backup is already
            // running" guard (IsBusy is pre-checked above). Show the localized string; the exception
            // message stays an untranslated developer diagnostic - do not localize the throw site.
            StatusText = Strings.Msg_Busy;
        }
        catch (Exception ex)
        {
            StatusText = string.Format(Strings.Status_BackupError, ex.Message);
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
        var path = BrowseForFolder(Strings.Msg_BrowseRestoreTarget);
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
            MessageBox.Show(Strings.Msg_RestoreSelectFirst, Strings.Msg_Caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(RestoreTargetPath))
        {
            MessageBox.Show(Strings.Msg_RestorePickFolder, Strings.Msg_Caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsBusy)
        {
            MessageBox.Show(Strings.Msg_OperationBusy, Strings.Msg_Caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StartRestore(RestoreJob.Job, SelectedBackupSet, RestoreTargetPath);
    }

    private async void StartRestore(BackupJob job, string setName, string targetPath)
    {
        IsBusy = true;
        StatusText = string.Format(Strings.Status_Restoring, setName);
        ProgressPercent = 0;
        ProgressText = string.Empty;
        // Hoisted out of the per-file handler (see StartRun): one lookup per restore, not per file.
        var progressTemplate = Strings.Status_ProgressRestore;
        var progress = new Progress<BackupProgress>(p =>
        {
            ProgressPercent = p.TotalFiles > 0 ? p.FilesDone * 100.0 / p.TotalFiles : 0;
            ProgressText = string.Format(progressTemplate, p.FilesDone, p.TotalFiles, p.CurrentFile);
        });

        try
        {
            var result = await Task.Run(() => App.Engine.RestoreBackup(
                job.Id, job.DestPath, setName, targetPath, progress, CancellationToken.None, null));
            StatusText = result.Success
                ? string.Format(Strings.Status_RestoreCompleted, result.FilesRestored,
                                BackupEngine.FormatBytes(result.BytesRestored))
                : result.Cancelled
                    ? Strings.Status_RestoreCancelled
                    : string.Format(Strings.Status_RestoreFailed, result.Error);
            if (result.Success)
            {
                ProgressPercent = 100;
            }
        }
        catch (Exception ex)
        {
            StatusText = string.Format(Strings.Status_RestoreError, ex.Message);
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
