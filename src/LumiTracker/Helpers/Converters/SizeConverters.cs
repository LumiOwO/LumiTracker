using LumiTracker.Controls;
using LumiTracker.Models;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class SizeWithRatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double size && parameter is string ratioStr)
            {
                double.TryParse(ratioStr, out double ratio);
                return Math.Max(size * ratio, 1.0);
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SizeWithRatioVariableConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2 || !(values[0] is double size) || !(values[1] is double ratio))
            {
                return 1.0;
            }

            return Math.Max(size * ratio, 1.0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool isAuto))
            {
                return GridLength.Auto;
            }

            return isAuto ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ExpandableCardListMaxHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 4)
            {
                return 0.0;
            }
            if (!(values[0] is ItemsControl itemsControl))
            {
                return 0.0;
            }
            if (!(values[1] is double headerHeight))
            {
                return 0.0;
            }
            if (!(values[2] is int)) // NumberToTouch
            {
                return 0.0;
            }
            if (!(values[3] is double)) // ItemsControl.ActualHeight
            {
                return 0.0;
            }

            var dataItems = itemsControl.ItemsSource as IEnumerable<CardList>;
            if (dataItems == null)
            {
                return 0.0;
            }

            int count = 0;
            int numExpanded = 0;
            foreach (var item in dataItems)
            {
                count++;
                numExpanded += item.IsExpanded ? 1 : 0;
            }

            double maxHeight = Math.Max(itemsControl.ActualHeight - count * headerHeight, 0.0);
            maxHeight /= Math.Max(numExpanded, 1);
            return maxHeight;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
