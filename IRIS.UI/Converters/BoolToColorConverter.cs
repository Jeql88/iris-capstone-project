using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IRIS.UI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string colors)
            {
                var colorParts = colors.Split('|');
                if (colorParts.Length == 2)
                {
                    var trueColor = colorParts[0];
                    var falseColor = colorParts[1];
                    return new BrushConverter().ConvertFrom(boolValue ? trueColor : falseColor) as SolidColorBrush ?? Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
