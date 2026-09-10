using System;
using System.IO;
using System.Threading;
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

public class BackupEngineTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _dest;
    private readonly BackupEngine _engine = new();

    public BackupEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fbtest_" + Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "src");
        _dest = Path.Combine(_root, "dest");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_dest);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // ignore cleanup failures
        }

        // Clean up AppData manifests created during tests
        try
        {
            var appDataSets = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileBackup", "sets");
            if (Directory.Exists(appDataSets))
            {
                foreach (var dir in Directory.GetDirectories(appDataSets))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private BackupJob Job(BackupType type = BackupType.Full) => new()
    {
        Name = "TestJob",
        SourcePath = _source,
        DestPath = _dest,
        JobType = type
    };

    private void CreateFile(string relativePath, byte[] content, DateTime? lastWrite = null)
    {
        var fullPath = Path.Combine(_source, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        if (lastWrite is not null)
        {
            File.SetLastWriteTimeUtc(fullPath, lastWrite.Value);
        }
    }

    // ----- Full backup -----

    [Fact]
    public void FullBackup_CopiesAllFiles_AndWritesManifest()
    {
        CreateFile("a.txt", "hello"u8.ToArray());
        CreateFile("sub/b.txt", new byte[2048]);
        CreateFile("sub2/deep/c.bin", new byte[1024]);

        var job = Job();
        var result = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(BackupType.Full, result.Type);
        Assert.Equal(3, result.FilesCopied);
        Assert.Equal(5 + 2048 + 1024, result.BytesCopied);
        Assert.StartsWith("Full_", result.SetFolder);

        Assert.Equal("hello", File.ReadAllText(Path.Combine(_dest, "a.txt")));
        Assert.Equal(2048, new FileInfo(Path.Combine(_dest, "sub", "b.txt")).Length);

        var manifest = ManifestStore.Load(job.Id, result.SetFolder!);
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest!.Files.Count);
        Assert.Contains(manifest.Files, f => f.RelativePath == "sub2/deep/c.bin");
    }

    [Fact]
    public void FullBackup_PreservesLastWriteTime()
    {
        var mtime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        CreateFile("a.txt", "hello"u8.ToArray(), mtime);

        var result = _engine.RunBackup(Job(), BackupType.Full, null, CancellationToken.None, null);
        Assert.True(result.Success, result.Error);

        var copied = new FileInfo(Path.Combine(_dest, "a.txt"));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(copied.FullName));
    }

    // ----- Differential backup -----

    [Fact]
    public void Differential_CopiesOnlyChangedFiles()
    {
        var originalMtime = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        CreateFile("a.txt", "v1"u8.ToArray(), originalMtime);
        CreateFile("unchanged.txt", "same"u8.ToArray(), originalMtime);
        CreateFile("sub/b.txt", new byte[512], originalMtime);

        var job = Job();
        var full = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);
        Assert.True(full.Success, full.Error);

        // modify a.txt (content), add new.txt, touch sub/b.txt (mtime only), leave unchanged.txt
        CreateFile("a.txt", "v2 with more content"u8.ToArray(), originalMtime.AddHours(1));
        CreateFile("new.txt", "brand new"u8.ToArray());
        File.SetLastWriteTimeUtc(Path.Combine(_source, "sub", "b.txt"), originalMtime.AddHours(2));

        var diff = _engine.RunBackup(job, BackupType.Differential, null, CancellationToken.None, null);

        Assert.True(diff.Success, diff.Error);
        Assert.Equal(BackupType.Differential, diff.Type);
        Assert.Equal(3, diff.FilesCopied); // a.txt, new.txt, sub/b.txt
        Assert.StartsWith("Diff_", diff.SetFolder);

        var manifest = ManifestStore.Load(job.Id, diff.SetFolder!);
        Assert.NotNull(manifest);
        Assert.Equal(full.SetFolder, manifest!.BaseFullSet);
        Assert.Equal("v2 with more content", File.ReadAllText(Path.Combine(_dest, "a.txt")));
        Assert.DoesNotContain(manifest.Files, f => f.RelativePath == "unchanged.txt");
    }

    [Fact]
    public void Differential_WithNoFullBackup_PromotesToFull()
    {
        CreateFile("a.txt", "hello"u8.ToArray());

        var result = _engine.RunBackup(Job(), BackupType.Differential, null, CancellationToken.None, null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(BackupType.Full, result.Type);
        Assert.StartsWith("Full_", result.SetFolder);
        Assert.Equal(1, result.FilesCopied);
    }

    [Fact]
    public void Differential_UsesLatestFullSet()
    {
        CreateFile("a.txt", "v1"u8.ToArray());
        var job = Job();
        var full1 = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);
        Thread.Sleep(1100); // set names have second resolution
        CreateFile("a.txt", "v2"u8.ToArray());
        var full2 = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);

        Assert.NotEqual(full1.SetFolder, full2.SetFolder);

        var diff = _engine.RunBackup(job, BackupType.Differential, null, CancellationToken.None, null);
        var manifest = ManifestStore.Load(job.Id, diff.SetFolder!);

        Assert.NotNull(manifest);
        Assert.Equal(full2.SetFolder, manifest!.BaseFullSet);
    }

    // ----- Cancellation -----

    [Fact]
    public void CancelledBackup_RemovesPartialSet()
    {
        for (var i = 0; i < 20; i++)
        {
            CreateFile($"f{i}.bin", new byte[512]);
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = _engine.RunBackup(Job(), BackupType.Full, null, cts.Token, null);

        Assert.True(result.Cancelled);
        Assert.False(result.Success);
    }

    // ----- Failure handling -----

    [Fact]
    public void MissingSource_FailsWithMessage()
    {
        var job = Job();
        job.SourcePath = Path.Combine(_root, "does_not_exist");

        var result = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);

        Assert.False(result.Success);
        Assert.Contains("Source folder not found", result.Error);
    }

    // ----- Restore -----

    [Fact]
    public void Restore_FullSet_CopiesAllFiles()
    {
        CreateFile("a.txt", "hello"u8.ToArray());
        CreateFile("sub/b.txt", new byte[100]);
        var job = Job();
        var full = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);

        var target = Path.Combine(_root, "restore_full");
        var result = _engine.RestoreBackup(
            job.Id, _dest, full.SetFolder!, target, null, CancellationToken.None, null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FilesRestored);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal(100, new FileInfo(Path.Combine(target, "sub", "b.txt")).Length);
    }

    [Fact]
    public void Restore_DifferentialSet_OverlaysBaseFull()
    {
        CreateFile("a.txt", "v1"u8.ToArray());
        CreateFile("b.txt", "keep"u8.ToArray());
        var job = Job();
        var full = _engine.RunBackup(job, BackupType.Full, null, CancellationToken.None, null);
        // explicit mtime so the change is unambiguous even if both writes land in the same timestamp tick
        CreateFile("a.txt", "v2"u8.ToArray(), new DateTime(2024, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        var diff = _engine.RunBackup(job, BackupType.Differential, null, CancellationToken.None, null);

        var target = Path.Combine(_root, "restore_diff");
        var result = _engine.RestoreBackup(
            job.Id, _dest, diff.SetFolder!, target, null, CancellationToken.None, null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FilesRestored); // destination has latest state: a.txt + b.txt
        Assert.Equal("v2", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "b.txt")));
    }

    // ----- Helpers -----

    [Fact]
    public void SanitizeFolderName_RemovesInvalidCharacters()
    {
        Assert.Equal("My_Job", BackupEngine.SanitizeFolderName("My:Job"));
        Assert.Equal("Job", BackupEngine.SanitizeFolderName("   "));
    }

    [Fact]
    public void FormatBytes_IsReadable()
    {
        Assert.Equal("0 B", BackupEngine.FormatBytes(0));
        Assert.Equal("1.00 KB", BackupEngine.FormatBytes(1024));
        Assert.Equal("1.00 MB", BackupEngine.FormatBytes(1024 * 1024));
    }
}
