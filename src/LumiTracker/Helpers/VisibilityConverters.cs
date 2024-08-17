using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class VisibilityConverterUtils
    {
        public static Visibility ToVisibility(string invisibleType, Func<bool> visibleCondition) 
        {
            if (visibleCondition())
            {
                return Visibility.Visible;
            }
            else if (invisibleType == "Collapsed")
            {
                return Visibility.Collapsed;
            }
            else if (invisibleType == "Hidden")
            {
                return Visibility.Hidden;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool isVisible && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => isVisible);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string s && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => s != "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double v && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => v != 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MultiBooleanAndToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || !values.All(v => v is bool) || !(parameter is string invisibleType))
                return false;

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => values.Cast<bool>().All(b => b));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HideNegativeIntValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int intValue && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            return VisibilityConverterUtils.ToVisibility(invisibleType, () => intValue >= 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
