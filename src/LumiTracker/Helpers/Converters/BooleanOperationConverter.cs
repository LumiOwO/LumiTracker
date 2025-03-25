using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class MultiBooleanAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || !values.All(v => v is bool))
                return false;

            return values.Cast<bool>().All(b => b);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool val))
            {
                throw new NotImplementedException();
            }

            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TrueFalseCondSwitchConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
            {
                return null;
            }

            var trueValue  = values[0];
            var falseValue = values[1];
            var condition  = values[2];

            bool? cond = null;
            if (condition is bool boolValue)
            {
                cond = boolValue;
            }
            else if (condition is string stringValue) 
            {
                cond = (stringValue == Boolean.TrueString) ? true
                    : (stringValue == Boolean.FalseString) ? false
                    : null;
            }

            if (cond != null)
            {
                return ((bool)cond) ? trueValue : falseValue;
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsNotNullValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsNullOrWhiteSpaceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string str))
            {
                return true;
            }
            return string.IsNullOrWhiteSpace(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
