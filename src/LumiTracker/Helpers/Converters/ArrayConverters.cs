using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class RangeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int startsFrom = 0;
            if (parameter is string paramString && int.TryParse(paramString, out int start))
            {
                startsFrom = start;
            }

            var range = new List<int>();
            if (value is int count)
            {
                for (int i = 0; i < count; i++)
                {
                    range.Add(i + startsFrom);
                }
            }

            return range;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
