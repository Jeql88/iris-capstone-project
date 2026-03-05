using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IRIS.UI.Converters
{
    /// <summary>
    /// Converts a bool to its inverse. Useful for two-way RadioButton binding.
    /// </summary>
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    /// <summary>
    /// Converts an integer to Visibility: 0 → Visible, > 0 → Collapsed.
    /// Used to show placeholder text when a collection is empty.
    /// </summary>
    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
