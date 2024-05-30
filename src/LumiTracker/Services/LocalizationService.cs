using LumiTracker.Config;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LumiTracker.Services
{ 
    public class LocalizationExtension : Binding
    {
        public LocalizationExtension(string name) : base("[" + name + "]")
        {
            Mode   = BindingMode.OneWay;
            Source = LocalizationSource.Instance;
        }
    }

    public interface ILocalizationService
    {
        void ChangeLanguage(string lang);
    }

    public class LocalizationService : ILocalizationService
    {
        public void ChangeLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang)) lang = "en-US";
            LocalizationSource.Instance.CurrentCulture = new CultureInfo(lang);
        }
    }
}
