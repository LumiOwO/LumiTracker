using LumiTracker.Config;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

using LumiTracker.ViewModels;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Views.Windows;
using LumiTracker.Views.Pages;
using Wpf.Ui;

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
        private MainWindowViewModel _mainWindowViewModel;

        public LocalizationService(MainWindowViewModel mainWindowViewModel, INavigationWindow mainWindow)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public void ChangeLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang)) lang = "en-US";
            LocalizationSource.Instance.CurrentCulture = new CultureInfo(lang);

            // Refresh main window menu items
            _mainWindowViewModel.StartViewItem!.Content    = LocalizationSource.Instance["Start"];
            _mainWindowViewModel.AboutViewItem!.Content    = LocalizationSource.Instance["About"];

            _mainWindowViewModel.SettingsViewItem!.Content = LocalizationSource.Instance["Settings"];

            _mainWindowViewModel.HomeTrayMenuItem!.Header  = LocalizationSource.Instance["Tray_Home"];
            _mainWindowViewModel.QuitTrayMenuItem!.Header  = LocalizationSource.Instance["Tray_Quit"];
        }
    }
}
