using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.ViewModels.Pages;
using System.Collections.ObjectModel;
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
                return Visibility.Hidden;
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
            {
                return Visibility.Collapsed;
            }

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => !values.Cast<bool>().All(b => b));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActionCardViewsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            ObservableCollection<ActionCardView> empty = [];
            if (values == null || values.Length != 3 || values[0] is not DeckStatistics)
                return empty;

            DeckStatistics stats = (values[0] as DeckStatistics)!;
            int index = (values[1] is int ? (int)values[1] : 0);
            if (index < 0 || index >= stats.AllBuildStats.Count) return empty;

            bool isLoaded = (values[2] is bool ? (bool)values[2] : false);
            return isLoaded ? stats.AllBuildStats[index].ActionCards : empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    
}
