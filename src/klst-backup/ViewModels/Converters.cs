using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KlstBackup.Resources;
using KlstBackup.Services;

namespace KlstBackup.ViewModels;

/// <summary>Converts bool to its inverse (for IsEnabled bindings).</summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>Maps a bool to Visibility inverted: true collapses, false shows
/// (e.g. a Pause button that must disappear while the task is paused).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Collapses an element unless the bound enum value equals the ConverterParameter.</summary>
public class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Renders a persisted UTC timestamp as local time in the Windows regional format.
/// Follows CurrentCulture (regional format), never the UI language.</summary>
public class UtcToLocalTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime dt ? TimeFormatter.UtcShort(dt) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps an enum value to its localized display name via the
/// <c>Enum_&lt;TypeName&gt;_&lt;MemberName&gt;</c> resource key. <see cref="DayOfWeek"/> comes
/// free from the OS culture data instead. Falls back to the raw member name, so it never
/// renders blank for a non-null enum. Display only - the bound value itself stays the raw enum,
/// which is what <see cref="EnumEqualsToVisibilityConverter"/> and the persisted
/// <c>JsonStringEnumConverter</c> English names both depend on.</summary>
public class EnumDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is DayOfWeek dow)
        {
            return CultureInfo.CurrentUICulture.DateTimeFormat.GetDayName(dow);
        }

        var key = $"Enum_{value.GetType().Name}_{value}";
        return Strings.ResourceManager.GetString(key) ?? value.ToString()!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Localizes the two English sentinels that <c>SchedulerService</c> persists into
/// <c>RunRecord.Message</c> ("Cancelled" / "Failed"). Everything else - backup-set folder names
/// and engine diagnostics - passes through verbatim. The writer is deliberately untouched, so
/// nothing localized reaches disk and history rows written before localization retro-localize
/// too.</summary>
public class RunMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Cancelled" => Strings.Enum_RunStatus_Cancelled,
            "Failed" => Strings.Enum_RunStatus_Failed,
            var s => s ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
