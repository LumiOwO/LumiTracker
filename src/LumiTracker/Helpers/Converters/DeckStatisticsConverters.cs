using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.ViewModels.Pages;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

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

    public class GetActiveDeckNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string none = $"<{Lang.UnknownDeck}>";
            if (values == null || values.Length != 3)
                return none;
            if (!(values[0] is ObservableCollection<DeckItem> deckItems))
                return none;
            if (!(values[1] is int index))
                return none;
            //if (!(values[2] is string __selectedDeckName)) // Only used for triggering
            //    return none;
            if (index < 0 || index >= deckItems.Count)
                return none;

            BuildStats stats = deckItems[index].Stats.SelectedBuildVersion;
            return DeckUtils.GetActualDeckName(stats);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DeckNameFromBuildStatsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(values[0] is BuildStats stats))
            {
                return $"";
            }
            //if (!(values[1] is string __deckname)) // Only used for trigger
            //{
            //    return $"";
            //}
            return DeckUtils.GetActualDeckName(stats);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BuildVersionNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(values[0] is BuildStats stats))
            {
                return $"<{Lang.UnknownDeck}>";
            }
            //if (!(values[1] is string __deckname)) // Only used for trigger
            //{
            //    return $"<{Lang.UnknownDeck}>";
            //}
            bool isSelected = false;
            if (parameter is string isSelectedStr && isSelectedStr == "1")
            {
                isSelected = true;
            }

            string timeStr = stats.Edit.CreatedAt.ToString("MM/dd HH:mm:ss");
            if (isSelected)
            {
                return timeStr;
            }
            else
            {
                string deckName = DeckUtils.GetActualDeckName(stats);
                return $"{deckName}  @{timeStr}";
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BuildVersionComboBoxSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; } = new DataTemplate();
        public DataTemplate SelectedItemTemplate { get; set; } = new DataTemplate();

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var itemToCheck = container;

            // Search up the visual tree, stopping at either a ComboBox or
            // a ComboBoxItem (or null). This will determine which template to use
            while (itemToCheck is not null
                    and not ComboBox
                    and not ComboBoxItem)
            {
                itemToCheck = VisualTreeHelper.GetParent(itemToCheck);
            }

            // If you stopped at a ComboBoxItem, you're in the dropdown
            var inDropDown = itemToCheck is ComboBoxItem;
            return inDropDown ? ItemTemplate : SelectedItemTemplate;
        }
    }

}
