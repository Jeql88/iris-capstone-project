using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IRIS.UI.Converters
{
    public class BoolToLabColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline ? Color.FromRgb(139, 0, 0) : Color.FromRgb(200, 200, 200);
            }
            return Color.FromRgb(200, 200, 200);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
