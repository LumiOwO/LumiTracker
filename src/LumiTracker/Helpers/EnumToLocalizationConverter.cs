using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

using LumiTracker.Config;
using Newtonsoft.Json.Linq;

namespace LumiTracker.Helpers
{
    public class EnumToLocalizationConverter : IValueConverter
    {
        static public string EnumToLocalization<T>(T value)
        {
            string typeStr = value!.GetType().Name.Substring(1);
            string valStr = value.ToString()!;
            return LocalizationSource.Instance[$"{typeStr}_{valStr}"];
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!Enum.IsDefined(value.GetType(), value))
            {
                throw new ArgumentException("EnumToLocalizationConverterValueMustBeAnEnum");
            }

            return EnumToLocalization(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
