using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IRIS.UI.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "online" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")),
                    "offline" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                    "idle" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAB308")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
