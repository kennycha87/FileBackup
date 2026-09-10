using System;
using System.IO;
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

public class ManifestStoreTests : IDisposable
{
    private readonly Guid _jobId = Guid.NewGuid();

    public void Dispose()
    {
        try
        {
            var setsFolder = ManifestStore.GetSetsFolder(_jobId);
            if (Directory.Exists(setsFolder))
            {
                Directory.Delete(setsFolder, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var manifest = new BackupManifest
        {
            JobName = "Docs",
            Type = BackupType.Full,
            CreatedUtc = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc),
            Files =
            {
                new FileEntry { RelativePath = "a/b.txt", Size = 123, LastWriteTimeUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
                new FileEntry { RelativePath = "c.txt", Size = 5, LastWriteTimeUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc) }
            }
        };

        ManifestStore.Save(manifest, _jobId, "Full_20260910_120000");

        var setsFolder = ManifestStore.GetSetsFolder(_jobId);
        Assert.True(File.Exists(Path.Combine(setsFolder, "Full_20260910_120000.json")));

        var loaded = ManifestStore.Load(_jobId, "Full_20260910_120000");
        Assert.NotNull(loaded);
        Assert.Equal("Docs", loaded!.JobName);
        Assert.Equal(BackupType.Full, loaded.Type);
        Assert.Equal(manifest.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(2, loaded.Files.Count);
        Assert.Equal("a/b.txt", loaded.Files[0].RelativePath);
        Assert.Equal(123, loaded.Files[0].Size);
    }

    [Fact]
    public void Load_MissingManifest_ReturnsNull()
    {
        Assert.Null(ManifestStore.Load(_jobId, "Full_20260910_120000"));
    }

    [Fact]
    public void FindLatestFullSet_PrefersManifestCreatedTimeOverSetName()
    {
        // The earlier-named set carries the newer manifest; the manifest must win.
        ManifestStore.Save(new BackupManifest { JobName = "j", Type = BackupType.Full, CreatedUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc) }, _jobId, "Full_20260901_120000");
        ManifestStore.Save(new BackupManifest { JobName = "j", Type = BackupType.Full, CreatedUtc = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc) }, _jobId, "Full_20260910_120000");

        Assert.Equal("Full_20260901_120000", ManifestStore.FindLatestFullSet(_jobId));
    }

    [Fact]
    public void ListSets_ReturnsSetsNewestFirst()
    {
        ManifestStore.Save(new BackupManifest { JobName = "j", Type = BackupType.Full, CreatedUtc = DateTime.UtcNow }, _jobId, "Full_20260910_080000");
        ManifestStore.Save(new BackupManifest { JobName = "j", Type = BackupType.Differential, BaseFullSet = "Full_20260910_080000", CreatedUtc = DateTime.UtcNow }, _jobId, "Diff_20260910_120000");
        ManifestStore.Save(new BackupManifest { JobName = "j", Type = BackupType.Full, CreatedUtc = DateTime.UtcNow }, _jobId, "Full_20260911_080000");

        var sets = ManifestStore.ListSets(_jobId);

        Assert.Equal(new[] { "Full_20260911_080000", "Diff_20260910_120000", "Full_20260910_080000" }, sets);
    }

    [Fact]
    public void ParseSetFolderName_ParsesStamp()
    {
        Assert.Equal(new DateTime(2026, 9, 10, 12, 0, 0), ManifestStore.ParseSetFolderName("Full_20260910_120000"));
        Assert.Equal(new DateTime(2026, 9, 10, 23, 59, 59), ManifestStore.ParseSetFolderName("Diff_20260910_235959_2"));
        Assert.Null(ManifestStore.ParseSetFolderName("backups"));
    }
}
