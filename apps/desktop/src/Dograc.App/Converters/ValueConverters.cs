using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Dograc.Core.Models;

namespace Dograc.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && (Invert ? v != Visibility.Visible : v == Visibility.Visible);
}

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null && parameter is not null && value.Equals(parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter is not null ? parameter : Binding.DoNothing;
}

public sealed class ServiceStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ServiceState state
            ? state switch
            {
                ServiceState.Ready => "Brush.Success",
                ServiceState.Checking or ServiceState.Starting or ServiceState.Degraded => "Brush.Warning",
                ServiceState.Error => "Brush.Danger",
                _ => "Brush.TextMuted"
            }
            : "Brush.TextMuted";

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
