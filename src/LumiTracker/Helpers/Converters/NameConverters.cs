using LumiTracker.Config;
using LumiTracker.ViewModels.Pages;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
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
            if (!(values[2] is string __selectedDeckName)) // Only used for triggering
                return none;
            if (index < 0 || index >= deckItems.Count)
                return none;
            return deckItems[index].Info.Name;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MainWindowTitleNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string title = (value as string)!;
            return $"{Lang.AppName} {Configuration.GetAssemblyVersion()} - {title}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OverlayWindowTitleNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string title = (value as string)!;
            return $"{Lang.AppName} - {title}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActionCardNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string unknown = Lang.UnknownCard;
            if (parameter is not int card_id || card_id < 0 || card_id >= (int)EActionCard.NumActions)
            {
                return unknown;
            }
            return Configuration.GetActionCardName(card_id);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
