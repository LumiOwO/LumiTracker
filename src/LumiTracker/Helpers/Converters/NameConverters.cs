using LumiTracker.Config;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class MainWindowTitleNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string title = (value as string)!;
            return $"{Lang.AppName} {Configuration.AppVersion.InfoName} - {title}";
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
