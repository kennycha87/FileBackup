using System.Globalization;
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

    static App()
    {
        // WPF binding StringFormat ignores CultureInfo.CurrentCulture: it derives the
        // culture from FrameworkElement.Language, which defaults to en-US
        // (dotnet/wpf#1946 / #10650). This override makes StringFormat=N0 and every
        // other default conversion follow the Windows regional format instead.
        //
        // A static constructor is the only correct place for this: it runs when Main() first
        // touches the App type - before InitializeComponent() and before any FrameworkElement
        // exists, which is exactly the ordering OverrideMetadata requires.
        var lang = System.Windows.Markup.XmlLanguage.GetLanguage(
            System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(lang));
    }

    /// <summary>
    /// Points <see cref="CultureInfo.CurrentUICulture"/> at the configured UI language. Must run
    /// before <see cref="MainWindow"/>/<see cref="ViewModels.MainViewModel"/> are constructed,
    /// because the view model reads resources in its field initializers.
    /// <see cref="CultureInfo.CurrentCulture"/> is deliberately left alone: it stays the Windows
    /// regional format and drives every date/time/number rendering.
    /// </summary>
    private static void ApplyUiLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;   // "Follow system": leave CurrentUICulture exactly as Windows set it
        }

        try
        {
            var ci = CultureInfo.GetCultureInfo(language);   // "zh-HK", "zh-CN", "en-US"
            CultureInfo.DefaultThreadCurrentUICulture = ci;   // covers scheduler/Task threads
            CultureInfo.CurrentUICulture = ci;
            // NOTE: CurrentCulture is intentionally NOT touched. It stays the Windows
            // regional format and drives every date/time/number rendering.
        }
        catch (CultureNotFoundException)
        {
            // A hand-edited config.json must never crash startup - fall back to system.
        }
    }

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
        ApplyUiLanguage(Config.Language);
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
