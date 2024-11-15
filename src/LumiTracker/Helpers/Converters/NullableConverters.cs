using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class NullableBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }
            return false;
        }
    }

    public class NullableIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = 0;
            if (value is int)
            {
                index = (int)value;
            }

            return Math.Max(index, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = 0;
            if (value is int)
            {
                index = (int)value;
            }

            return Math.Max(index, 0);
        }
    }
}