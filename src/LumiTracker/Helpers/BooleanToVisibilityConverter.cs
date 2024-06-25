using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}
