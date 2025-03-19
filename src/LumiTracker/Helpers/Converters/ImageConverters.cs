using LumiTracker.Config;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class CharacterIdToAvatarUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int character_id = -1;
            if (value != null && value is int)
            {
                character_id = (int)value;
            }

            if (character_id < 0 || character_id >= (int)ECharacterCard.NumCharacters)
            {
                character_id = 0;
            }
            return $"pack://siteoforigin:,,,/assets/images/avatars/{character_id}.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CharacterIdToAvatarVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int character_id = -1;
            if (value != null && value is int)
            {
                character_id = (int)value;
            }

            Visibility res;
            if (character_id < 0 || character_id >= (int)ECharacterCard.NumCharacters)
            {
                res = Visibility.Hidden;
            }
            else
            {
                res = Visibility.Visible;
            }
            return res;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActionCardIdToSnapshotUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int card_id = -1;
            if (value != null && value is int)
            {
                card_id = (int)value;
            }

            if (card_id < 0 || card_id >= (int)EActionCard.NumActions)
            {
                card_id = 0;
            }
            return $"pack://siteoforigin:,,,/assets/images/snapshots/{card_id}.jpg";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActionCardIdToSnapshotVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int card_id = -1;
            if (value != null && value is int)
            {
                card_id = (int)value;
            }

            Visibility res;
            if (card_id < 0 || card_id >= (int)EActionCard.NumActions)
            {
                res = Visibility.Hidden;
            }
            else
            {
                res = Visibility.Visible;
            }
            return res;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CostTypeToDiceUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ECostType costType = ECostType.Any;
            if (value != null && value is ECostType)
            {
                costType = (ECostType)value;
            }

            return $"pack://siteoforigin:,,,/assets/images/costs/{costType.ToString()}.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
