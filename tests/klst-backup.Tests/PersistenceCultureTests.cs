using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

/// <summary>
/// End-to-end proof that the i18n feature cannot corrupt anything written to disk - the
/// "Persistence non-regression" half of plan Phase 9, guarding risks R5 (a localized or
/// culture-formatted value reaching config.json, a manifest, a log file or a backup-set name) and
/// R6 (a set name that no longer round-trips under a non-Gregorian calendar, which would make
/// <see cref="ManifestStore.FindLatestFullSet"/> choose the wrong differential baseline and quietly
/// corrupt backups). Decisions D6 (logs, level tokens, engine messages, JSON property names and
/// serialized enum values are never localized) and D9 (<see cref="RunRecord.Message"/> keeps the raw
/// set-folder name / the English "Cancelled"/"Failed" sentinels) are asserted as they appear in the
/// real bytes on disk.
/// <para>
/// Every case drives the PRODUCTION write path - <see cref="SchedulerService.RunJob"/> ->
/// <see cref="BackupEngine.RunBackup"/> -> <see cref="ManifestStore"/> / <see cref="ConfigService"/>
/// / <see cref="LogService"/> - synchronously on the calling thread while a
/// <see cref="CultureScope"/> makes both <c>CurrentCulture</c> and <c>CurrentUICulture</c> hostile.
/// Nothing is reimplemented here. The scheduler's timer thread is deliberately never started:
/// <see cref="CultureScope"/> is thread-local by design, so a pre-existing timer thread would not
/// observe it and the run would silently execute under the ambient culture.
/// </para>
/// <para>
/// Assertions read RAW TEXT - JSON tokens, log lines, on-disk file names - and never a deserialized
/// <see cref="AppConfig"/> or <see cref="BackupManifest"/> for the artifact checks: a converter
/// normalizes a malformed value and hides exactly the corruption this suite exists to catch. The
/// one deliberate exception is the cross-locale reload case, whose whole point is that the typed
/// model comes back intact.
/// </para>
/// <para>
/// I/O isolation: source, destination, log root and config.json all live in a freshly created temp
/// directory that <see cref="Dispose"/> removes, following the <see cref="IDisposable"/> convention
/// of <c>BackupEngineTests</c>. The real <c>%APPDATA%\FileBackup\config.json</c>, <c>\logs\</c> and
/// every real destination folder are never read, written or deleted. The one unavoidable exception
/// is the manifest, because <see cref="ManifestStore"/> hard-codes its root: it is confined to this
/// instance's own GUID folder and removed in <see cref="Dispose"/>. This class shares the
/// <c>ManifestStoreRoot</c> xUnit collection with the other classes that touch that root, so they
/// run sequentially relative to each other instead of deleting each other's manifests mid-test.
/// </para>
/// </summary>
[Collection("ManifestStoreRoot")]
public sealed class PersistenceCultureTests : IDisposable
{
    // The job name deliberately contains a space: RunLog.Sanitize only replaces characters that are
    // invalid in a Windows file name, so the name reaches the log file name verbatim.
    private const string JobName = "Persistence Probe";
    private const string ExpectedLogFileNamePrefix = JobName;

    // Non-zero seconds on purpose: System.Text.Json omits ":ss" from a TimeOnly whose seconds are
    // zero, and the persisted contract under test here is the full invariant HH:mm:ss shape.
    private static readonly TimeOnly JobTime = new(2, 30, 45);
    private const string ExpectedTimeText = "02:30:45";
    private const ScheduleType JobSchedule = ScheduleType.Weekly;
    private const DayOfWeek JobWeekDay = DayOfWeek.Wednesday;
    private const int JobDayOfMonth = 15;

    // Two nesting levels so the recursive walk (BackupEngine.CollectFiles) and the byte total are
    // both non-trivial, while staying tiny enough that a run finishes in well under a second.
    private const int ExpectedFiles = 4;
    private const long ExpectedBytes = 27 + 14 + 2048 + 512;

    private static readonly string[] ExpectedRelativePaths =
    {
        "nested/child.txt",
        "nested/deep/blob.bin",
        "readme.txt",
        "tail.log"
    };

    /// <summary>Tolerance around the clock readings taken adjacent to the run. A minute is ample
    /// for a sub-second run and still ~543 years smaller than the Buddhist-era offset it must catch.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(60);

    // NOTE ON [0-9] vs \d: in .NET \d matches every Unicode decimal digit (\p{Nd}), which includes
    // the Thai digits and the full-width digits. Using \d here would happily accept precisely the
    // corruption this suite exists to reject, so every pattern spells [0-9] out.
    private static readonly Regex SetNamePattern =
        new(@"^(Full|Diff)_[0-9]{8}_[0-9]{6}(_[0-9]+)?$", RegexOptions.Compiled);
    private static readonly Regex SetArtifactPattern =
        new(@"^(Full|Diff)_[0-9]{8}_[0-9]{6}(_[0-9]+)?\.json$", RegexOptions.Compiled);
    private static readonly Regex UtcPattern =
        new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z$", RegexOptions.Compiled);
    private static readonly Regex DurationPattern =
        new(@"^[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?$", RegexOptions.Compiled);
    private static readonly Regex TimeOnlyPattern =
        new(@"^([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$", RegexOptions.Compiled);
    private static readonly Regex IntegerPattern = new(@"^-?[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex LogMonthPattern = new(@"^[0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex LogFileNamePattern = new(
        "^" + Regex.Escape(ExpectedLogFileNamePrefix) + @"_(?<stamp>[0-9]{8}_[0-9]{6})\.log$", RegexOptions.Compiled);
    private static readonly Regex LogLinePattern = new(
        @"^(?<stamp>[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}) (?<level>\S+) +(?<message>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex InvariantByteCountPattern =
        new(@"[0-9]+\.[0-9]{2} KB", RegexOptions.Compiled);

    private static readonly string[] LogLevelTokens = { "INFO", "WARN", "ERROR" };

    /// <summary>Every enum member name the persisted models can hold. <c>JsonStringEnumConverter</c>
    /// writes these; a localized display name must never appear among them.</summary>
    private static readonly HashSet<string> EnglishEnumNames = new(StringComparer.Ordinal)
    {
        "Full", "Differential",                                     // BackupType
        "Daily", "Weekly", "Monthly",                               // ScheduleType
        "Completed", "CompletedWithWarnings", "Failed", "Cancelled", // RunStatus
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" // DayOfWeek
    };

    private static readonly HashSet<string> EnumPropertyNames = new(StringComparer.Ordinal)
    {
        "ScheduleType", "JobType", "Type", "Status", "WeekDay"
    };

    private readonly string _root;
    private readonly string _source;
    private readonly string _dest;
    private readonly string _logRoot;
    private readonly string _configPath;

    /// <summary>
    /// <see cref="ManifestStore"/> is static and hard-codes <c>%APPDATA%\FileBackup\sets\&lt;jobId&gt;</c>,
    /// so a real backup run has nowhere else to put its manifest. It is confined to a GUID minted by
    /// this test instance, contains nothing but that one run's artifacts, and is removed in
    /// <see cref="Dispose"/> - the same convention <c>ManifestStoreTests</c> already uses. The real
    /// config.json, the real logs\ tree and every real destination folder stay untouched.
    /// </summary>
    private readonly string _setsFolder;

    private readonly Guid _jobId = Guid.NewGuid();

    /// <summary>The only non-ASCII characters tolerated anywhere in a persisted artifact are those
    /// already present in the paths this test injects (a Windows profile name is outside our
    /// control). On a plain-ASCII profile this set is empty, making the check "no non-ASCII at
    /// all"; elsewhere it stays truthful instead of failing for an unrelated reason.</summary>
    private readonly HashSet<char> _allowedNonAscii;

    public PersistenceCultureTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fbculture_" + Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _dest = Path.Combine(_root, "dest");
        _logRoot = Path.Combine(_root, "logs");
        _configPath = Path.Combine(_root, "config.json");
        _setsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileBackup", "sets", _jobId.ToString("N"));

        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_dest);

        _allowedNonAscii = new HashSet<char>(_root.Where(c => c >= '\u0080'));

        SeedSourceFiles();
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

        // Only this test's own GUID-scoped manifest folder - never the sets root and never another
        // job's folder, so the real %APPDATA%\FileBackup tree is net-zero once we are gone.
        try
        {
            if (Directory.Exists(_setsFolder))
            {
                Directory.Delete(_setsFolder, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    // ------------------------------------------------------------------ 1. backup-set name

    [Theory]
    [InlineData("th-TH")] // Buddhist calendar: an unpinned stamp renders 2026 as 2569
    [InlineData("zh-CN")]
    [InlineData("zh-HK")]
    public void PersistedSetFolderName_UnderHostileCulture_StaysGregorian(string culture)
    {
        var run = Execute(culture);

        // The name the engine reported and persisted into config.json / the log.
        AssertSetNameIsGregorian(run.SetName, "The backup-set name reported by the engine", culture, run.Window);

        // And the artifact names as they really exist on disk: one run writes exactly one manifest.
        Assert.Equal(new[] { run.SetName + ".json" }, run.ManifestFileNames);
        foreach (var fileName in run.ManifestFileNames)
        {
            Assert.True(SetArtifactPattern.IsMatch(fileName),
                $"The manifest artifact '{fileName}' written under culture '{culture}' is not an invariant " +
                $"'Full_yyyyMMdd_HHmmss.json' / 'Diff_yyyyMMdd_HHmmss.json' name.");

            AssertSetNameIsGregorian(
                Path.GetFileNameWithoutExtension(fileName),
                $"The manifest artifact '{fileName}' on disk",
                culture,
                run.Window);
        }

        // D9: the newest persisted RunRecord.Message is the raw set-folder name, unlocalized.
        Assert.Equal(run.SetName, run.Job.History[^1].Message);
    }

    // ------------------------------------------------------------------ 2. config.json

    [Theory]
    [InlineData("th-TH")]
    [InlineData("zh-CN")]
    [InlineData("zh-HK")]
    public void ConfigJson_UnderHostileCulture_IsInvariantAsciiAndEnglishEnums(string culture)
    {
        var run = Execute(culture);

        // Both writes are checked: the plain pre-run save (this is where the job's TimeOnly, its
        // enums and the Language tag first reach the disk) and the post-run save (LastRunUtc plus
        // the whole RunRecord history written by SchedulerService).
        AssertJsonInvariant(run.InitialConfigText, "config.json (pre-run save)", culture, run.Window);
        AssertJsonInvariant(run.ConfigText, "config.json (post-run save)", culture, run.Window);

        using var document = JsonDocument.Parse(run.ConfigText);
        var root = document.RootElement;
        Assert.Equal(culture, root.GetProperty("Language").GetString());
        Assert.False(root.GetProperty("StartWithWindows").GetBoolean());

        var jobJson = Assert.Single(root.GetProperty("Jobs").EnumerateArray().ToList());
        Assert.Equal(_jobId.ToString(), jobJson.GetProperty("Id").GetString());
        Assert.Equal(JobName, jobJson.GetProperty("Name").GetString());
        Assert.Equal(_source, jobJson.GetProperty("SourcePath").GetString());
        Assert.Equal(_dest, jobJson.GetProperty("DestPath").GetString());
        Assert.True(jobJson.GetProperty("Enabled").GetBoolean());

        // Enum values keep their English names on disk; only the display layer localizes them.
        Assert.Equal("Weekly", jobJson.GetProperty("ScheduleType").GetString());
        Assert.Equal("Wednesday", jobJson.GetProperty("WeekDay").GetString());
        Assert.Equal("Full", jobJson.GetProperty("JobType").GetString());
        Assert.Equal(JobDayOfMonth, jobJson.GetProperty("DayOfMonth").GetInt32());

        // The HH:mm value the user typed must not gain a locale time separator or a 12-hour suffix.
        Assert.Equal(ExpectedTimeText, jobJson.GetProperty("Time").GetString());

        var history = jobJson.GetProperty("History").EnumerateArray().ToList();
        Assert.Equal(1, history.Count); // one RunJob call appends exactly one RunRecord
        foreach (var record in history)
        {
            Assert.Equal(JobName, record.GetProperty("JobName").GetString());
            Assert.Equal("Full", record.GetProperty("Type").GetString());
            Assert.Equal("Completed", record.GetProperty("Status").GetString());
            Assert.Equal(ExpectedFiles, record.GetProperty("FilesCopied").GetInt32());
            Assert.Equal(ExpectedBytes, record.GetProperty("BytesCopied").GetInt64());
        }

        Assert.Equal(run.SetName, history[^1].GetProperty("Message").GetString());
    }

    // ------------------------------------------------------------------ 3. manifest

    [Theory]
    [InlineData("th-TH")]
    [InlineData("zh-CN")]
    [InlineData("zh-HK")]
    public void ManifestJson_UnderHostileCulture_IsInvariantAsciiAndEnglishEnums(string culture)
    {
        var run = Execute(culture);
        var artifact = $"manifest artifact '{run.SetName}.json'";

        AssertJsonInvariant(run.ManifestText, artifact, culture, run.Window);

        using var document = JsonDocument.Parse(run.ManifestText);
        var root = document.RootElement;
        Assert.Equal(JobName, root.GetProperty("JobName").GetString());
        Assert.Equal("Full", root.GetProperty("Type").GetString());
        Assert.Null(root.GetProperty("BaseFullSet").GetString()); // a full set has no baseline

        var files = root.GetProperty("Files").EnumerateArray().ToList();
        Assert.Equal(ExpectedFiles, files.Count);
        Assert.Equal(
            ExpectedRelativePaths,
            files.Select(f => f.GetProperty("RelativePath").GetString()!)
                 .OrderBy(p => p, StringComparer.Ordinal)
                 .ToList());

        foreach (var file in files)
        {
            // Sizes are plain ASCII integers, timestamps are ISO-8601 UTC ending in Z.
            AssertFormat(IntegerPattern, file.GetProperty("Size").GetRawText(), $"{artifact} 'Size'", culture);
            AssertFormat(UtcPattern, file.GetProperty("LastWriteTimeUtc").GetString()!, $"{artifact} 'LastWriteTimeUtc'", culture);
        }
    }

    // ------------------------------------------------------------------ 4. log artifacts

    [Theory]
    [InlineData("th-TH")]
    [InlineData("zh-CN")]
    [InlineData("zh-HK")]
    public void RunLogArtifacts_UnderHostileCulture_UseGregorianStampsAndEnglishTokens(string culture)
    {
        var run = Execute(culture);

        // LogService.WriteAppEvent (app_yyyyMMdd.log at the root) is not part of a backup run, so
        // the log root must hold nothing but the Gregorian yyyyMM run directory - no stray artifact.
        Assert.Empty(run.LogRootFileNames);
        Assert.NotEmpty(run.LogDirectoryNames);
        var allowedMonths = new[]
        {
            Month(run.Window.LocalStart),
            Month(run.Window.LocalEnd)
        };
        foreach (var directoryName in run.LogDirectoryNames)
        {
            AssertFormat(LogMonthPattern, directoryName, "The run-log directory name", culture);
            Assert.True(allowedMonths.Contains(directoryName, StringComparer.Ordinal),
                $"The run-log directory was created as '{directoryName}' under culture '{culture}' but the run " +
                $"happened in Gregorian month '{Month(run.Window.LocalStart)}'. A non-Gregorian calendar (th-TH " +
                $"is Buddhist: 202609 becomes 256909) reached the log path instead of CultureInfo.InvariantCulture, " +
                $"so log folders no longer sort chronologically.");
        }

        // One run opens exactly one RunLog, which creates exactly one file.
        Assert.Single(run.LogFileNames);
        foreach (var relativePath in run.LogFileNames)
        {
            var fileName = Path.GetFileName(relativePath);
            var match = LogFileNamePattern.Match(fileName);
            Assert.True(match.Success,
                $"The run-log file '{fileName}' written under culture '{culture}' does not match " +
                $"'{ExpectedLogFileNamePrefix}_yyyyMMdd_HHmmss.log'.");

            var stamp = DateTime.ParseExact(
                match.Groups["stamp"].Value, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
            AssertInWindow(stamp, run.Window, utc: false, $"The run-log file name '{fileName}'", culture);
        }

        Assert.NotEmpty(run.LogLines);
        foreach (var line in run.LogLines)
        {
            var match = LogLinePattern.Match(line);
            Assert.True(match.Success,
                $"The log line '{line}' written under culture '{culture}' does not match the invariant " +
                $"'yyyy-MM-dd HH:mm:ss LEVEL message' shape.");

            var stamp = DateTime.ParseExact(
                match.Groups["stamp"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
            AssertInWindow(stamp, run.Window, utc: false, $"The timestamp of the log line '{line}'", culture);

            // D6: level tokens are diagnostics vocabulary and are never localized.
            Assert.Contains(match.Groups["level"].Value, LogLevelTokens);

            // D6: message PROSE staying English is correct and is not asserted here - only that no
            // localized character or non-ASCII digit reached the file.
            AssertInvariantText(line, $"The log line '{line}'", culture);
        }

        // The set name and the byte count are echoed into the log; both must be the invariant
        // renderings. FormatBytes is pinned to InvariantCulture precisely because its output lands
        // in these files, so the decimal separator must stay '.' whatever the regional format says.
        var joined = string.Join(Environment.NewLine, run.LogLines);
        Assert.Contains(run.SetName, joined, StringComparison.Ordinal);
        AssertSetNameIsGregorian(run.SetName, "The set name echoed into the run log", culture, run.Window);
        Assert.True(InvariantByteCountPattern.IsMatch(joined),
            $"The run log written under culture '{culture}' carries no invariant 'N.NN KB' byte count. " +
            $"BackupEngine.FormatBytes is pinned to CultureInfo.InvariantCulture because its output is echoed " +
            $"verbatim into these files; a regional decimal separator would corrupt every byte total in the log.");
    }

    // ------------------------------------------------------------------ 5. cross-locale reload

    [Theory]
    [InlineData("th-TH")]
    [InlineData("zh-CN")]
    [InlineData("zh-HK")]
    public void Config_WrittenUnderHostileCulture_ReloadsIntactUnderEnUs(string culture)
    {
        var run = Execute(culture);

        AppConfig reloaded;
        using (new CultureScope("en-US"))
        {
            // Written hostile, read neutral: any culture-sensitive value in the JSON would either
            // fail to parse (ConfigService.Load swallows that and hands back an empty config) or
            // come back changed.
            reloaded = new ConfigService(_configPath).Load();
        }

        AssertConfigIntact(reloaded, run, culture, "read back under en-US");

        AppConfig hostileReload;
        using (new CultureScope(culture))
        {
            hostileReload = new ConfigService(_configPath).Load();
        }

        // ...and the same bytes read back under the culture that wrote them, so the pin is proven
        // in both directions rather than only "hostile write, neutral read".
        AssertConfigIntact(hostileReload, run, culture, $"read back under {culture}");
    }

    // ------------------------------------------------------------------ the run itself

    /// <summary>
    /// Performs a real backup through the production code path with both cultures hostile, then
    /// snapshots every persisted artifact as raw text.
    /// </summary>
    private HostileRun Execute(string culture)
    {
        var job = new BackupJob
        {
            Id = _jobId,
            Name = JobName,
            SourcePath = _source,
            DestPath = _dest,
            Enabled = true,
            ScheduleType = JobSchedule,
            Time = JobTime,
            WeekDay = JobWeekDay,
            DayOfMonth = JobDayOfMonth,
            JobType = BackupType.Full
        };

        var config = new AppConfig { StartWithWindows = false, Language = culture };
        config.Jobs.Add(job);

        var configService = new ConfigService(_configPath);
        var logService = new LogService(_logRoot);
        var scheduler = new SchedulerService(config, configService, new BackupEngine(), logService);

        var attempts = 0;
        var initialConfigText = string.Empty;
        var configText = string.Empty;
        var manifestText = string.Empty;
        var window = default(RunWindow);
        BackupResult? result = null;

        while (true)
        {
            attempts++;

            // CultureScope pins CurrentCulture and CurrentUICulture on THIS thread only, and
            // SchedulerService.RunJob is documented for synchronous Task.Run use - so calling it
            // directly keeps the entire write path (set name, manifest, config.json, log file)
            // inside the hostile culture. No timer thread is started.
            using (new CultureScope(culture))
            {
                if (attempts == 1)
                {
                    configService.Save(config);
                    initialConfigText = File.ReadAllText(_configPath);
                }

                var beforeLocal = DateTime.Now;
                var beforeUtc = DateTime.UtcNow;

                result = scheduler.RunJob(job, scheduled: true, progress: null);

                var afterLocal = DateTime.Now;
                var afterUtc = DateTime.UtcNow;

                window = new RunWindow(
                    beforeLocal - Slack, afterLocal + Slack,
                    beforeUtc - Slack, afterUtc + Slack);

                configText = File.ReadAllText(_configPath);
                manifestText = TryReadManifestText(result.SetFolder ?? string.Empty) ?? string.Empty;
            }

            if (manifestText.Length > 0)
            {
                break;
            }

            // The shared %APPDATA%\FileBackup\sets root is deleted wholesale by
            // BackupEngineTests.Dispose, and xUnit runs test classes in parallel, so the manifest
            // artifact can vanish between the engine writing it and this test reading it. That is a
            // defect of the pre-existing suite, not of the product: re-run rather than report a
            // false corruption. (It is also why ManifestStoreTests.FindLatestFullSet_* only fails in
            // a full-suite run and passes in isolation.)
            Assert.True(attempts < MaxAttempts,
                $"The manifest artifact for set '{result?.SetFolder}' disappeared from '{_setsFolder}' on " +
                $"{attempts.ToString(CultureInfo.InvariantCulture)} consecutive attempts: another test class is " +
                $"deleting the shared %APPDATA%\\FileBackup\\sets root while this test runs.");
        }

        var outcome = result!;
        Assert.True(outcome.Success, $"The backup run under culture '{culture}' failed: {outcome.Error}");
        Assert.False(outcome.Cancelled);
        Assert.Equal(BackupType.Full, outcome.Type);
        Assert.Equal(ExpectedFiles, outcome.FilesCopied);
        Assert.Equal(ExpectedBytes, outcome.BytesCopied);
        Assert.Equal(0, outcome.Warnings);

        return new HostileRun
        {
            Culture = culture,
            Attempts = attempts,
            Job = job,
            Window = window,
            InitialConfigText = initialConfigText,
            ConfigText = configText,
            SetName = outcome.SetFolder!,
            ManifestText = manifestText,
            ManifestFileNames = SnapshotNames(
                () => Directory.GetFiles(_setsFolder, "*.json").Select(f => Path.GetFileName(f)!)),
            LogDirectoryNames = SnapshotNames(
                () => Directory.GetDirectories(_logRoot).Select(d => Path.GetFileName(d)!)),
            LogRootFileNames = SnapshotNames(
                () => Directory.GetFiles(_logRoot).Select(f => Path.GetFileName(f)!)),
            LogFileNames = SnapshotNames(
                () => Directory.GetFiles(_logRoot, "*.log", SearchOption.AllDirectories)
                               .Select(f => Path.GetRelativePath(_logRoot, f))),
            LogLines = Directory.GetFiles(_logRoot, "*.log", SearchOption.AllDirectories)
                                .OrderBy(f => f, StringComparer.Ordinal)
                                .SelectMany(File.ReadAllLines)
                                .ToArray()
        };
    }

    private void SeedSourceFiles()
    {
        Write("readme.txt", "invariant persistence probe"u8.ToArray());
        Write("nested/child.txt", "nested payload"u8.ToArray());
        Write("nested/deep/blob.bin", new byte[2048]);
        Write("tail.log", new byte[512]);
    }

    private void Write(string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(_source, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
    }

    private string? TryReadManifestText(string setName)
    {
        if (setName.Length == 0)
        {
            return null;
        }

        try
        {
            var path = Path.Combine(_setsFolder, setName + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // lost the race with the parallel wipe; Execute re-runs
        }
    }

    /// <summary>Deterministic, failure-tolerant snapshot of a directory listing.</summary>
    private static string[] SnapshotNames(Func<IEnumerable<string>> enumerate)
    {
        try
        {
            return enumerate().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private void AssertConfigIntact(AppConfig config, HostileRun run, string culture, string how)
    {
        // ConfigService.Load swallows every parse error and hands back an empty config, so a value
        // that a foreign culture cannot parse shows up here rather than as an exception.
        Assert.True(config.Jobs.Count == 1,
            $"The config written under culture '{run.Culture}' did not survive being {how}: " +
            $"{config.Jobs.Count.ToString(CultureInfo.InvariantCulture)} job(s) came back instead of exactly 1.");

        Assert.Equal(culture, config.Language);
        Assert.False(config.StartWithWindows);

        var job = Assert.Single(config.Jobs);
        Assert.Equal(_jobId, job.Id);
        Assert.Equal(JobName, job.Name);
        Assert.Equal(_source, job.SourcePath);
        Assert.Equal(_dest, job.DestPath);
        Assert.True(job.Enabled);
        Assert.Equal(JobSchedule, job.ScheduleType);
        Assert.Equal(JobTime, job.Time);
        Assert.Equal(JobWeekDay, job.WeekDay);
        Assert.Equal(JobDayOfMonth, job.DayOfMonth);
        Assert.Equal(BackupType.Full, job.JobType);

        Assert.NotNull(job.LastRunUtc);
        Assert.Equal(DateTimeKind.Utc, job.LastRunUtc!.Value.Kind);
        AssertInWindow(job.LastRunUtc.Value, run.Window, utc: true, $"The reloaded 'LastRunUtc' ({how})", culture);

        Assert.Equal(1, job.History.Count); // one RunJob call appends exactly one RunRecord
        foreach (var record in job.History)
        {
            Assert.Equal(JobName, record.JobName);
            Assert.Equal(BackupType.Full, record.Type);
            Assert.Equal(RunStatus.Completed, record.Status);
            Assert.Equal(ExpectedFiles, record.FilesCopied);
            Assert.Equal(ExpectedBytes, record.BytesCopied);
            Assert.Equal(DateTimeKind.Utc, record.StartedUtc.Kind);
            AssertInWindow(record.StartedUtc, run.Window, utc: true, $"A reloaded 'StartedUtc' ({how})", culture);
            Assert.True(record.Duration > TimeSpan.Zero && record.Duration < TimeSpan.FromMinutes(5),
                $"A reloaded 'Duration' came back as {record.Duration} ({how}).");
            AssertSetNameIsGregorian(record.Message, $"A reloaded 'RunRecord.Message' ({how})", culture, run.Window);
        }

        // D9: the newest persisted message is the raw set-folder name.
        Assert.Equal(run.SetName, job.History[^1].Message);
    }

    // ------------------------------------------------------------------ assertion helpers

    /// <summary>Clock readings taken immediately around the run, widened by <see cref="Slack"/>.
    /// Deriving every expectation from an adjacent clock reading instead of hardcoding today's date
    /// is what keeps these cases free of midnight and month-boundary flakiness.</summary>
    private readonly record struct RunWindow(
        DateTime LocalStart, DateTime LocalEnd, DateTime UtcStart, DateTime UtcEnd);

    /// <summary>Everything one hostile run persisted, captured as raw text and raw file names.</summary>
    private sealed class HostileRun
    {
        public required string Culture { get; init; }
        public required BackupJob Job { get; init; }
        public required RunWindow Window { get; init; }
        public required string InitialConfigText { get; init; }
        public required string ConfigText { get; init; }
        public required string SetName { get; init; }
        public required string ManifestText { get; init; }
        public required string[] ManifestFileNames { get; init; }
        public required string[] LogDirectoryNames { get; init; }
        public required string[] LogRootFileNames { get; init; }
        public required string[] LogFileNames { get; init; }
        public required string[] LogLines { get; init; }
    }

    private static string Month(DateTime value) => value.ToString("yyyyMM", CultureInfo.InvariantCulture);

    private static string FormatInvariant(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static void AssertFormat(Regex pattern, string value, string what, string culture)
    {
        Assert.True(pattern.IsMatch(value),
            $"{what} was persisted as '{value}' under culture '{culture}', which does not match the invariant " +
            $"pattern '{pattern}'. Everything written to disk is pinned to CultureInfo.InvariantCulture (plan D6/R5).");
    }

    private static void AssertInWindow(DateTime actual, RunWindow window, bool utc, string what, string culture)
    {
        var start = utc ? window.UtcStart : window.LocalStart;
        var end = utc ? window.UtcEnd : window.LocalEnd;

        Assert.True(actual >= start && actual <= end,
            $"{what} was persisted as '{FormatInvariant(actual)}' under culture '{culture}', outside the Gregorian " +
            $"window [{FormatInvariant(start)} .. {FormatInvariant(end)}] read from the clock immediately around the " +
            $"run. A large offset means the value was formatted with the ambient culture's calendar instead of " +
            $"CultureInfo.InvariantCulture.");
    }

    /// <summary>
    /// The assertion that gives this suite its teeth. A shape check alone is not enough: a Buddhist
    /// stamp such as "Full_25690911_143012" still matches ^(Full|Diff)_[0-9]{8}_[0-9]{6}$, so the
    /// name is additionally pushed through the PRODUCTION reader
    /// <see cref="ManifestStore.ParseSetFolderName"/> and required to land next to the run (plan R6).
    /// </summary>
    private static void AssertSetNameIsGregorian(string setName, string what, string culture, RunWindow window)
    {
        Assert.True(SetNamePattern.IsMatch(setName),
            $"{what} was persisted as '{setName}' under culture '{culture}'. A backup-set name must always be " +
            $"'Full_yyyyMMdd_HHmmss' or 'Diff_yyyyMMdd_HHmmss' written with CultureInfo.InvariantCulture.");

        var parsed = ManifestStore.ParseSetFolderName(setName);
        Assert.True(parsed is not null,
            $"{what} ('{setName}', written under culture '{culture}') cannot be read back by " +
            $"ManifestStore.ParseSetFolderName, so it can never be selected as a differential baseline.");

        var stamp = parsed!.Value;
        var runYear = window.LocalStart.Year;
        Assert.True(Math.Abs(stamp.Year - runYear) <= 1,
            $"{what} was persisted as '{setName}' under culture '{culture}' and parses to the Gregorian year " +
            $"{stamp.Year.ToString(CultureInfo.InvariantCulture)}, but the run happened in " +
            $"{runYear.ToString(CultureInfo.InvariantCulture)} ({FormatInvariant(window.LocalStart)}). The ambient " +
            $"culture's calendar reached the disk - th-TH is Buddhist, where 2026 renders as 2569 - instead of " +
            $"CultureInfo.InvariantCulture. Such a set mis-sorts and makes ManifestStore.FindLatestFullSet pick the " +
            $"wrong differential baseline (plan risk R6).");

        AssertInWindow(stamp, window, utc: false, $"{what} ('{setName}')", culture);
    }

    /// <summary>
    /// Asserts that a persisted JSON artifact is culture-free in its raw bytes: no localized
    /// character survives in the file text, in a property name, or in a string value after JSON
    /// unescaping (which is where a \uXXXX-escaped leak would otherwise hide); every number is a
    /// plain ASCII integer; and every date/time/enum property has its invariant shape.
    /// </summary>
    private void AssertJsonInvariant(string rawText, string artifact, string culture, RunWindow window)
    {
        AssertInvariantText(rawText, $"{artifact} raw text", culture);
        Assert.False(string.IsNullOrWhiteSpace(rawText), $"{artifact} is empty.");

        using var document = JsonDocument.Parse(rawText);
        Walk(document.RootElement, "$");

        void Walk(JsonElement element, string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childPath = path + "." + property.Name;
                        AssertInvariantText(property.Name, $"{artifact} property name '{childPath}'", culture);
                        Check(property.Name, property.Value, childPath);
                        Walk(property.Value, childPath);
                    }

                    break;

                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                        index++;
                    }

                    break;

                case JsonValueKind.String:
                    AssertInvariantText(element.GetString()!, $"{artifact} string value at '{path}'", culture);
                    break;

                case JsonValueKind.Number:
                    AssertFormat(IntegerPattern, element.GetRawText(), $"{artifact} number at '{path}'", culture);
                    break;
            }
        }

        void Check(string name, JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                return; // numbers are covered by Walk; null/bool carry no culture
            }

            var text = value.GetString()!;
            var what = $"{artifact} '{name}' at {path}";

            if (name.EndsWith("Utc", StringComparison.Ordinal))
            {
                AssertFormat(UtcPattern, text, what, culture);
                var parsed = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                Assert.Equal(DateTimeKind.Utc, parsed.Kind);
                AssertInWindow(parsed, window, utc: true, what, culture);
                return;
            }

            switch (name)
            {
                case "Duration":
                    AssertFormat(DurationPattern, text, what, culture);
                    Assert.True(TimeSpan.Parse(text, CultureInfo.InvariantCulture) < TimeSpan.FromMinutes(5),
                        $"{what} was persisted as '{text}' under culture '{culture}', which is not a plausible run duration.");
                    break;

                case "Time":
                    AssertFormat(TimeOnlyPattern, text, what, culture);
                    Assert.Equal(ExpectedTimeText, text);
                    break;

                case "Message":
                    // D9: SchedulerService persists the raw set-folder name on success and the
                    // English sentinels otherwise; a display-only converter does the localizing.
                    if (text is not ("Cancelled" or "Failed"))
                    {
                        AssertSetNameIsGregorian(text, what, culture, window);
                    }

                    break;

                default:
                    if (EnumPropertyNames.Contains(name))
                    {
                        Assert.True(EnglishEnumNames.Contains(text),
                            $"{what} was persisted as '{text}' under culture '{culture}'. Serialized enum values keep " +
                            $"their English names on disk (JsonStringEnumConverter); only the display layer localizes " +
                            $"them (plan D6).");
                    }

                    break;
            }
        }
    }

    /// <summary>Fails when persisted text carries any character that could only have come from a
    /// localized render: Thai script (whose block also holds the Thai digits), CJK ideographs /
    /// punctuation / full-width forms (Traditional and Simplified Chinese, and the full-width
    /// digits), any other non-ASCII letter, or a decimal digit outside ASCII 0-9.</summary>
    private void AssertInvariantText(string text, string what, string culture)
    {
        var hostile = FindHostileCharacter(text);
        Assert.True(hostile is null,
            $"{what} contains a localized {hostile} after a run under culture '{culture}'. Nothing written to disk " +
            $"may carry localized text or non-ASCII digits: config.json, manifests, log files and backup-set names " +
            $"are pinned to CultureInfo.InvariantCulture (plan D6, risk R5).");
    }

    private string? FindHostileCharacter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < '\u0080' || _allowedNonAscii.Contains(c))
            {
                continue;
            }

            // Supplementary-plane CJK arrives as a surrogate pair; classify it as one code point.
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                var codePoint = char.ConvertToUtf32(c, text[i + 1]);
                if ((codePoint >= 0x20000 && codePoint <= 0x2FA1F) || (codePoint >= 0x30000 && codePoint <= 0x323AF))
                {
                    return $"CJK ideograph (supplementary plane) U+{codePoint.ToString("X", CultureInfo.InvariantCulture)} at index {i}";
                }

                i++;
                continue;
            }

            var kind = Classify(c);
            if (kind is not null)
            {
                return $"{kind} U+{((int)c).ToString("X4", CultureInfo.InvariantCulture)} ('{c}') at index {i}";
            }
        }

        return null;
    }

    private static string? Classify(char c) => c switch
    {
        >= '\u0E00' and <= '\u0E7F' => "Thai script character (the Thai digits U+0E50-U+0E59 live in this block)",
        >= '\u3000' and <= '\u303F' => "CJK punctuation character",
        >= '\u3040' and <= '\u30FF' => "Japanese kana character",
        >= '\u3400' and <= '\u4DBF' => "CJK ideograph (extension A)",
        >= '\u4E00' and <= '\u9FFF' => "CJK ideograph (Traditional or Simplified Chinese)",
        >= '\uAC00' and <= '\uD7AF' => "Hangul syllable",
        >= '\uF900' and <= '\uFAFF' => "CJK compatibility ideograph",
        >= '\uFF00' and <= '\uFFEF' => "full-width/half-width form (the full-width digits U+FF10-U+FF19 live here)",
        _ when char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber => "non-ASCII decimal digit",
        _ when char.IsLetter(c) => "non-ASCII letter",
        _ => null
    };
}
