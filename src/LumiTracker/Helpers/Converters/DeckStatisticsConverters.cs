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

    public class KeyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string key && parameter is string inverseString))
            {
                throw new NotImplementedException();
            }

            bool inverse = (inverseString == Boolean.TrueString);
            return VisibilityConverterUtils.ToVisibility("Hidden", () => (key != DeckUtils.UnknownCharactersKey) ^ inverse);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RecordsBeforeImportVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2 || !values.All(v => v is bool) || !(parameter is string invisibleType))
                return false;

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => !values.Cast<bool>().All(b => b));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
