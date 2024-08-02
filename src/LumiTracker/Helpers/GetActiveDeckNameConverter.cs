using LumiTracker.Config;
using LumiTracker.ViewModels.Pages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class GetActiveDeckNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string none = LocalizationSource.Instance["None"];
            if (values == null || values.Length != 3)
                return none;
            if (!(values[0] is ObservableCollection<DeckInfo> deckInfos))
                return none;
            if (!(values[1] is int index))
                return none;
            if (!(values[2] is string __selectedDeckName)) // Only used for triggering
                return none;
            if (index < 0 || index >= deckInfos.Count)
                return none;
            return deckInfos[index].Name;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
