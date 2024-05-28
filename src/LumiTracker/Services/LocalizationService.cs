using LumiTracker.Config;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumiTracker.Services
{
    public interface ILocalizationService
    {
        void ChangeLanguage(string lang);
    }

    public class LocalizationService : ILocalizationService
    {
        public void ChangeLanguage(string lang)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(lang);

            // TODO: refresh resources
        }
    }
}
