using System.Threading;
using System.Windows;
using KlstBackup.Models;
using KlstBackup.Services;
using KlstBackup.Views;

namespace KlstBackup;

/// <summary>
/// Application entry point. Wires up services, enforces a single instance,
/// and opens the main window.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    public static LogService Log { get; private set; } = null!;
    public static ConfigService ConfigService { get; private set; } = null!;
    public static AppConfig Config { get; private set; } = null!;
    public static BackupEngine Engine { get; private set; } = null!;
    public static SchedulerService Scheduler { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "FileBackup_SingleInstance_" + Environment.UserName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Log = new LogService();
        ConfigService = new ConfigService();
        Config = ConfigService.Load();
        Engine = new BackupEngine();
        Scheduler = new SchedulerService(Config, ConfigService, Engine, Log);

        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        Scheduler.Start();
        Log.WriteAppEvent("Application started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Scheduler.Stop();
        try
        {
            ConfigService.Save(Config);
        }
        catch
        {
            // best effort on shutdown
        }

        Log.WriteAppEvent("Application exited.");
        base.OnExit(e);
        _singleInstanceMutex?.ReleaseMutex();
    }
}
