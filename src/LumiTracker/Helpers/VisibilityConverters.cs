using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool isVisible && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            if (isVisible)
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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanNotThenToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool notVisible && parameter is string invisibleType))
            {
                throw new NotImplementedException();
            }

            if (!notVisible)
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

            bool isVisible = values.Cast<bool>().All(b => b);
            if (isVisible)
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

            if (intValue >= 0)
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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
