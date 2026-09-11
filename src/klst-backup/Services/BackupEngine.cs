using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using KlstBackup.Models;

namespace KlstBackup.Services;

/// <summary>
/// Executes full and differential backups and restores.
/// A differential run copies only files that are new or changed (size or
/// LastWriteTimeUtc differ) relative to the latest full backup's manifest.
/// </summary>
public class BackupEngine
{
    public BackupResult RunBackup(BackupJob job, BackupType requestedType, IProgress<BackupProgress>? progress,
        CancellationToken ct, RunLog? log, BackupTask? task = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new BackupResult { Type = requestedType };

        try
        {
            if (string.IsNullOrWhiteSpace(job.SourcePath) || !Directory.Exists(job.SourcePath))
            {
                throw new DirectoryNotFoundException($"Source folder not found: {job.SourcePath}");
            }

            if (string.IsNullOrWhiteSpace(job.DestPath))
            {
                throw new ArgumentException("Destination folder is not configured.");
            }

            var destPath = GetJobRoot(job);
            Directory.CreateDirectory(destPath);

            var type = requestedType;
            if (type == BackupType.Differential && ManifestStore.FindLatestFullSet(job.Id) is null)
            {
                log?.Warning("No full backup exists yet. Running a full backup instead of a differential.");
                type = BackupType.Full;
            }

            var prefix = type == BackupType.Full ? "Full_" : "Diff_";
            var setName = prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            if (type == BackupType.Full)
            {
                CopyFull(job, destPath, setName, progress, ct, log, result, task);
            }
            else
            {
                CopyDifferential(job, destPath, setName, progress, ct, log, result, task);
            }

            result.Success = true;
            result.Type = type;
            result.SetFolder = setName;
            result.Duration = sw.Elapsed;
            log?.Info($"Backup completed: {result.FilesCopied} file(s), {FormatBytes(result.BytesCopied)}, {result.Warnings} warning(s), set '{result.SetFolder}'.");
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            result.Success = false;
            result.Duration = sw.Elapsed;
            log?.Warning("Backup cancelled.");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.Duration = sw.Elapsed;
            log?.Error("Backup failed: " + ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Restores from the destination (which always holds the latest state) into the target folder.
    /// The manifest from AppData is used for reporting but all files come from the destination.
    /// </summary>
    public RestoreResult RestoreBackup(Guid jobId, string destPath, string setName, string targetPath,
        IProgress<BackupProgress>? progress, CancellationToken ct, RunLog? log)
    {
        var sw = Stopwatch.StartNew();
        var result = new RestoreResult();

        try
        {
            if (!Directory.Exists(destPath))
            {
                throw new DirectoryNotFoundException($"Destination folder not found: {destPath}");
            }

            // Read manifest for reporting (optional — restore works from destination regardless)
            var manifest = ManifestStore.Load(jobId, setName);
            if (manifest is not null)
            {
                log?.Info($"Restoring from set '{setName}' ({manifest.Type}).");
            }

            Directory.CreateDirectory(targetPath);
            CopyDirectoryContents(destPath, targetPath, progress, ct, log, result);

            result.Success = true;
            result.Duration = sw.Elapsed;
            log?.Info($"Restore completed: {result.FilesRestored} file(s), {result.Warnings} warning(s).");
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            result.Duration = sw.Elapsed;
            log?.Warning("Restore cancelled.");
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.Duration = sw.Elapsed;
            log?.Error("Restore failed: " + ex.Message);
            return result;
        }
    }

    private void CopyFull(BackupJob job, string destPath, string setName, IProgress<BackupProgress>? progress,
        CancellationToken ct, RunLog? log, BackupResult result, BackupTask? task)
    {
        var sourceRoot = job.SourcePath!;
        var files = CollectFiles(sourceRoot);
        long totalBytes = files.Sum(f => f.Size);

        var manifest = new BackupManifest
        {
            JobName = job.Name,
            Type = BackupType.Full,
            CreatedUtc = DateTime.UtcNow
        };

        var done = 0;
        long copiedBytes = 0;
        foreach (var file in files)
        {
            CheckTaskState(task, ct);
            var relative = Path.GetRelativePath(sourceRoot, file.Info.FullName).Replace('\\', '/');
            try
            {
                CopyFile(file.Info.FullName, destPath, relative, overwrite: true);
                manifest.Files.Add(new FileEntry
                {
                    RelativePath = relative,
                    Size = file.Size,
                    LastWriteTimeUtc = file.Info.LastWriteTimeUtc
                });
                copiedBytes += file.Size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Warnings++;
                log?.Warning($"Skipped '{relative}': {ex.Message}");
            }

            done++;
            UpdateTaskProgress(task, done, files.Count, copiedBytes, totalBytes, relative);
            progress?.Report(new BackupProgress
            {
                CurrentFile = relative,
                FilesDone = done,
                TotalFiles = files.Count,
                BytesCopied = copiedBytes,
                TotalBytes = totalBytes
            });
        }

        result.FilesCopied = done;
        result.BytesCopied = copiedBytes;
        ManifestStore.Save(manifest, job.Id, setName);
    }

    private void CopyDifferential(BackupJob job, string destPath, string setName, IProgress<BackupProgress>? progress,
        CancellationToken ct, RunLog? log, BackupResult result, BackupTask? task)
    {
        var fullSetName = ManifestStore.FindLatestFullSet(job.Id)!;
        var baseManifest = ManifestStore.Load(job.Id, fullSetName) ?? new BackupManifest();
        var baseline = baseManifest.Files.ToDictionary(
            f => f.RelativePath,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var sourceRoot = job.SourcePath!;
        var files = CollectFiles(sourceRoot);

        // Pass 1: determine changed files so progress totals are accurate.
        var changed = new List<(SourceFile File, string Relative)>();
        foreach (var file in files)
        {
            CheckTaskState(task, ct);
            var relative = Path.GetRelativePath(sourceRoot, file.Info.FullName).Replace('\\', '/');
            baseline.TryGetValue(relative, out var baseEntry);
            var isChanged = baseEntry is null
                            || baseEntry.Size != file.Size
                            || baseEntry.LastWriteTimeUtc != file.Info.LastWriteTimeUtc;
            if (isChanged)
            {
                changed.Add((file, relative));
            }
        }

        long totalBytes = changed.Sum(f => f.File.Size);
        var manifest = new BackupManifest
        {
            JobName = job.Name,
            Type = BackupType.Differential,
            CreatedUtc = DateTime.UtcNow,
            BaseFullSet = fullSetName
        };

        // Pass 2: copy only the changed files to the destination (updating the mirror).
        var done = 0;
        long copiedBytes = 0;
        foreach (var (file, relative) in changed)
        {
            CheckTaskState(task, ct);
            try
            {
                CopyFile(file.Info.FullName, destPath, relative, overwrite: true);
                manifest.Files.Add(new FileEntry
                {
                    RelativePath = relative,
                    Size = file.Size,
                    LastWriteTimeUtc = file.Info.LastWriteTimeUtc
                });
                copiedBytes += file.Size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Warnings++;
                log?.Warning($"Skipped '{relative}': {ex.Message}");
            }

            done++;
            UpdateTaskProgress(task, done, changed.Count, copiedBytes, totalBytes, relative);
            progress?.Report(new BackupProgress
            {
                CurrentFile = relative,
                FilesDone = done,
                TotalFiles = changed.Count,
                BytesCopied = copiedBytes,
                TotalBytes = totalBytes
            });
        }

        result.FilesCopied = done;
        result.BytesCopied = copiedBytes;
        ManifestStore.Save(manifest, job.Id, setName);
    }

    /// <summary>
    /// Blocks while the owning <see cref="BackupTask"/> is paused, and throws
    /// <see cref="OperationCanceledException"/> when the run is cancelled - either through
    /// <paramref name="ct"/> or because the task itself was cancelled (e.g. from the
    /// dashboard). With no owning task this reduces to <c>ct.ThrowIfCancellationRequested()</c>.
    /// </summary>
    private static void CheckTaskState(BackupTask? task, CancellationToken ct)
    {
        if (task is null)
        {
            ct.ThrowIfCancellationRequested();
            return;
        }

        if (task.IsCancelled)
        {
            throw new OperationCanceledException(ct);
        }

        ct.ThrowIfCancellationRequested();

        // Cancel() does not clear IsPaused, so the cancelled check must run inside the
        // wait loop as well or a task cancelled while paused would spin forever.
        while (task.IsPaused)
        {
            if (task.IsCancelled)
            {
                throw new OperationCanceledException(ct);
            }

            ct.ThrowIfCancellationRequested();
            Thread.Sleep(100);
        }
    }

    /// <summary>Mirrors copy progress onto the owning task so the dashboard shows live numbers.</summary>
    private static void UpdateTaskProgress(BackupTask? task, int filesDone, int totalFiles,
        long bytesCopied, long totalBytes, string currentFile)
    {
        if (task is null)
        {
            return;
        }

        task.FilesProcessed = filesDone;
        task.TotalFiles = totalFiles;
        task.BytesProcessed = bytesCopied;
        task.TotalBytes = totalBytes;
        task.CurrentFile = currentFile;
    }

    private void CopyDirectoryContents(string sourceDir, string targetDir, IProgress<BackupProgress>? progress,
        CancellationToken ct, RunLog? log, RestoreResult result)
    {
        var files = CollectFiles(sourceDir);
        long totalBytes = files.Sum(f => f.Size);

        var done = 0;
        long restoredBytes = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, file.Info.FullName);

            try
            {
                CopyFile(file.Info.FullName, targetDir, relative, overwrite: true);
                restoredBytes += file.Size;
                result.FilesRestored++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Warnings++;
                log?.Warning($"Could not restore '{relative}': {ex.Message}");
            }

            done++;
            progress?.Report(new BackupProgress
            {
                CurrentFile = relative,
                FilesDone = done,
                TotalFiles = files.Count,
                BytesCopied = restoredBytes,
                TotalBytes = totalBytes
            });
        }

        result.BytesRestored += restoredBytes;
    }

    private static void CopyFile(string sourceFullPath, string targetRoot, string relativePath, bool overwrite)
    {
        var target = Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var targetDir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(EnsureLongPath(targetDir));
        }

        File.Copy(EnsureLongPath(sourceFullPath), EnsureLongPath(target), overwrite);
        try
        {
            File.SetLastWriteTimeUtc(EnsureLongPath(target), File.GetLastWriteTimeUtc(EnsureLongPath(sourceFullPath)));
        }
        catch
        {
            // timestamp preservation is nice to have, not critical
        }
    }

    private static List<SourceFile> CollectFiles(string root)
    {
        var files = new List<SourceFile>();
        Collect(new DirectoryInfo(root), files);
        return files;

        static void Collect(DirectoryInfo dir, List<SourceFile> into)
        {
            try
            {
                foreach (var file in dir.EnumerateFiles())
                {
                    try
                    {
                        into.Add(new SourceFile(file, file.Length));
                    }
                    catch
                    {
                        // unreadable size, skip
                    }
                }
            }
            catch
            {
                // unreadable directory, skip
            }

            try
            {
                foreach (var sub in dir.EnumerateDirectories())
                {
                    Collect(sub, into);
                }
            }
            catch
            {
                // unreadable directory, skip
            }
        }
    }

    private readonly record struct SourceFile(FileInfo Info, long Size);

    /// <summary>Destination root for a job's backup sets.</summary>
    public static string GetJobRoot(BackupJob job)
    {
        return job.DestPath;
    }

    public static string SanitizeFolderName(string name)
    {
        var parts = name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries);
        var safe = string.Join("_", parts);
        return string.IsNullOrWhiteSpace(safe) ? "Job" : safe;
    }

    private static string EnsureLongPath(string path)
    {
        if (!Path.IsPathRooted(path) || path.StartsWith(@"\\?\"))
        {
            return path;
        }

        return path.Length > 240
            ? (path.StartsWith(@"\\") ? @"\\?\UNC\" + path[2..] : @"\\?\" + path)
            : path;
    }

    /// <summary>Renders a byte count for display. Pinned to the invariant culture because the
    /// result is echoed verbatim into the run log files.</summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => string.Format(CultureInfo.InvariantCulture, "{0:0.00} GB", bytes / 1073741824.0),
        >= 1L << 20 => string.Format(CultureInfo.InvariantCulture, "{0:0.00} MB", bytes / 1048576.0),
        >= 1L << 10 => string.Format(CultureInfo.InvariantCulture, "{0:0.00} KB", bytes / 1024.0),
        _ => $"{bytes} B"
    };
}
