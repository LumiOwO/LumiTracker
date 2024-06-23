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
using LumiTracker.ViewModels.Pages;
using LumiTracker.Helpers;
using System.Windows.Controls;

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
        private StartViewModel      _startViewModel;
        private StartPage           _startPage;

        public LocalizationService(MainWindowViewModel mainWindowViewModel, StartViewModel startViewModel, StartPage startPage)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _startViewModel      = startViewModel;
            _startPage           = startPage;
        }

        public void ChangeLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang)) lang = "en-US";
            LocalizationSource.Instance.CurrentCulture = new CultureInfo(lang);

            // Refresh main window menu items
            _mainWindowViewModel.StartViewItem!.Content    = LocalizationSource.Instance["Start"];
            _mainWindowViewModel.RankViewItem!.Content     = LocalizationSource.Instance["Rank"];
            _mainWindowViewModel.AboutViewItem!.Content    = LocalizationSource.Instance["About"];

            _mainWindowViewModel.SettingsViewItem!.Content = LocalizationSource.Instance["Settings"];

            _mainWindowViewModel.HomeTrayMenuItem!.Header  = LocalizationSource.Instance["Tray_Home"];
            _mainWindowViewModel.QuitTrayMenuItem!.Header  = LocalizationSource.Instance["Tray_Quit"];

            // Refresh start page
            _startViewModel.ClientTypes = Enum.GetValues(typeof(EClientType)).Cast<EClientType>();

            int index = _startPage.ClientTypeComboBox.SelectedIndex;
            _startPage.ClientTypeComboBox.SelectedIndex = -1;
            _startPage.ClientTypeComboBox.SelectedIndex = index;

            {
                Binding binding = new Binding("ViewModel.GameWatcherState");
                binding.Mode = BindingMode.OneWay;
                binding.Converter = (IValueConverter)_startPage.Resources["EnumToLocalizationConverter"];
                _startPage.GameWatcherStateText.SetBinding(TextBlock.TextProperty, binding);
            }
        }
    }
}
