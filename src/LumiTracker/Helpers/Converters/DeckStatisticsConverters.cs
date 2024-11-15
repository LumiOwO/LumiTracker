using LumiTracker.Config;
using LumiTracker.Models;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class IsGoodWinRateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double winRate && winRate >= 0.5)
            {
                return true;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
