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
            if (values == null || values.Length != 6)
            {
                return 0.0;
            }
            if (!(values[0] is ItemsControl itemsControl))
            {
                return 0.0;
            }
            if (!(values[1] is int index))
            {
                return 0.0;
            }
            if (!(values[2] is double headerHeight))
            {
                return 0.0;
            }
            if (!(values[3] is double cardItemHeight))
            {
                return 0.0;
            }
            if (!(values[4] is int)) // NumberToTouch
            {
                return 0.0;
            }
            if (!(values[5] is double)) // ItemsControl.ActualHeight
            {
                return 0.0;
            }

            var dataItems = itemsControl.ItemsSource as IEnumerable<CardList>;
            if (dataItems == null)
            {
                return 0.0;
            }

            int count = itemsControl.AlternationCount;
            double remainHeight = Math.Max(itemsControl.ActualHeight - count * headerHeight, 0.0);

            int numExpanded = 0;
            int lastExpanded = -1;
            int[] numElems = new int[count];

            int i = 0;
            foreach (var item in dataItems)
            {
                if (item.IsExpanded)
                {
                    numExpanded++;
                    lastExpanded = i;
                    numElems[i] = item.Data.Count; // Number of elements in list
                }
                else
                {
                    // If current collapsed, set maxHeight to 0
                    if (index == i) return 0.0;
                }
                i++;
            }
            if (numExpanded == 1) return remainHeight;

            // Compute minimum maxHeight
            double minimumMaxHeight = remainHeight / Math.Max(numExpanded, 1);
            if (index != lastExpanded)
            {
                return minimumMaxHeight;
            }

            // Compute last expanded item
            double lastHeight = remainHeight;
            for (int j = 0; j < lastExpanded; j++)
            {
                double height = Math.Clamp(numElems[j] * cardItemHeight, 0, minimumMaxHeight);
                lastHeight -= height;
            }
            return Math.Clamp(lastHeight, minimumMaxHeight, remainHeight);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
