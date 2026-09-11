using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace KlstBackup.Services;

/// <summary>Log writer for a single backup run; one file per run.</summary>
public sealed class RunLog : IDisposable
{
    private readonly StreamWriter? _writer;
    private readonly object _sync = new();

    public string FilePath { get; }

    public RunLog(string logRoot, string name)
    {
        try
        {
            // Persisted log paths and timestamps are pinned to the invariant culture so that
            // log folders sort chronologically under every regional format and calendar.
            var dir = Path.Combine(logRoot, DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir,
                $"{Sanitize(name)}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.log");
            _writer = new StreamWriter(FilePath, append: false, Encoding.UTF8);
        }
        catch
        {
            FilePath = string.Empty;
        }
    }

    public void Info(string message) => Write("INFO ", message);

    public void Warning(string message) => Write("WARN ", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        if (_writer is null)
        {
            return;
        }

        lock (_sync)
        {
            try
            {
                _writer.WriteLine(
                    $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {level} {message}");
                _writer.Flush();
            }
            catch
            {
                // never let logging break a backup
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string Sanitize(string name)
    {
        var parts = name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries);
        var safe = string.Join("_", parts);
        return string.IsNullOrWhiteSpace(safe) ? "run" : safe;
    }
}

/// <summary>Creates run logs and records application level events.</summary>
public class LogService
{
    private readonly string _root;

    public LogService() : this(DefaultRoot)
    {
    }

    public LogService(string root)
    {
        _root = root;
        try
        {
            Directory.CreateDirectory(root);
        }
        catch
        {
            // logging is best effort
        }
    }

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileBackup", "logs");

    public RunLog BeginRun(string jobName) => new(_root, jobName);

    public void WriteAppEvent(string message)
    {
        try
        {
            var path = Path.Combine(_root,
                $"app_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
            File.AppendAllText(path,
                $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
        }
        catch
        {
            // best effort
        }
    }
}
