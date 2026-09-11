using System.Windows;
using KlstBackup.ViewModels;

namespace KlstBackup.Views;

/// <summary>
/// Floating dashboard window that lists active backup tasks in real time
/// (progress, elapsed time, current file) and exposes Pause/Resume/Cancel
/// controls per task. Data context is <see cref="DashboardViewModel"/>,
/// which owns the refresh timer and the queue commands.
/// </summary>
public partial class DashboardWindow : Window
{
    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
