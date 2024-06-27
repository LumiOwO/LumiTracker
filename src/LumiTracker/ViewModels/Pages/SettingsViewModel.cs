using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.ViewModels.Windows;
using System.Globalization;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LumiTracker.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private ELanguage _currentLanguage;

        [ObservableProperty]
        private EClosingBehavior _currentClosingBehavior;

        [ObservableProperty]
        private ApplicationTheme _currentTheme;

        private readonly ILocalizationService _localizationService;

        public SettingsViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() { }

        private void InitializeViewModel()
        {
            Enum.TryParse(Configuration.Data.lang.Replace('-', '_'), out ELanguage curLang);
            CurrentLanguage = curLang;
            Enum.TryParse(Configuration.Data.closing_behavior, out EClosingBehavior curBehavior);
            CurrentClosingBehavior = curBehavior;
            Enum.TryParse(Configuration.Data.theme, out ApplicationTheme curTheme);
            CurrentTheme = curTheme;

            _isInitialized = true;
        }

        [RelayCommand]
        private void OnChangeLanguage(string lang)
        {
            Enum.TryParse(lang.Replace('-', '_'), out ELanguage curLang);
            CurrentLanguage = curLang;
            
            _localizationService.ChangeLanguage(lang);

            Configuration.Data.lang = lang;
            Configuration.Save();
        }

        [RelayCommand]
        private void OnChangeClosingBehavior(string closing_behavior)
        {
            Enum.TryParse(closing_behavior, out EClosingBehavior curBehavior);
            CurrentClosingBehavior = curBehavior;

            Configuration.Data.closing_behavior = closing_behavior;
            Configuration.Save();
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            var cfg = Configuration.Data;
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;
                    cfg.theme = "Light";

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    cfg.theme = "Dark";

                    break;

                // TODO: Follow system
                //SystemThemeWatcher.Watch(this);

            }
            Configuration.Save();
        }

        
    }
}
