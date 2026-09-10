using System.Windows;
using KlstBackup.ViewModels;

namespace KlstBackup.Views;

/// <summary>Add/edit dialog for a backup job.</summary>
public partial class JobEditDialog : Window
{
    public JobEditViewModel ViewModel { get; }

    public JobEditDialog(JobEditViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ScheduleCombo.ItemsSource = Enum.GetValues<Models.ScheduleType>();
        WeekDayCombo.ItemsSource = Enum.GetValues<DayOfWeek>();
        DayOfMonthCombo.ItemsSource = Enumerable.Range(1, 31).ToList();
        JobTypeCombo.ItemsSource = Enum.GetValues<Models.BackupType>();
    }

    private void OnBrowseSource(object sender, System.Windows.RoutedEventArgs e)
    {
        var path = MainViewModel.BrowseForFolder("Select the source folder to back up");
        if (path is not null)
        {
            ViewModel.SourcePath = path;
        }
    }

    private void OnBrowseDest(object sender, System.Windows.RoutedEventArgs e)
    {
        var path = MainViewModel.BrowseForFolder("Select the destination folder for backups");
        if (path is not null)
        {
            ViewModel.DestPath = path;
        }
    }

    private void OnScheduleTypeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // ensure dependent combos pick sensible values when switching frequency
        if (ViewModel is null || ScheduleCombo.SelectedIndex < 0)
        {
            return;
        }
    }

    private void OnOk(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ViewModel.Accept())
        {
            DialogResult = true;
        }
    }
}
