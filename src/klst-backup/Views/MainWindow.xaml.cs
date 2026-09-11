using System.ComponentModel;
using System.Windows;
using KlstBackup.Resources;
using KlstBackup.Services;
using KlstBackup.ViewModels;
using WinForms = System.Windows.Forms;

namespace KlstBackup.Views;

/// <summary>
/// Main window. Closing the window hides it to the system tray; the tray icon
/// provides Open/Exit and the app keeps running scheduled backups.
/// </summary>
public partial class MainWindow : Window
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private bool _reallyExit;
    private bool _balloonShown;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Icon = AppIcon.CreateImageSource();

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = AppIcon.CreateIcon(),
            Text = Strings.Tray_Tooltip,
            Visible = true
        };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Strings.Tray_Open, null, (_, _) => RestoreFromTray());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Strings.Tray_Exit, null, (_, _) =>
        {
            _reallyExit = true;
            Close();
        });
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            e.Cancel = true;
            Hide();
            if (!_balloonShown)
            {
                _balloonShown = true;
                _notifyIcon.ShowBalloonTip(2500, Strings.App_Title,
                    Strings.Tray_BalloonBody,
                    WinForms.ToolTipIcon.Info);
            }

            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosing(e);
    }
}
