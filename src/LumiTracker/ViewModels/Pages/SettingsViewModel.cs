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
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private ELanguage _currentLanguage = ELanguage.zh_HANS;

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
            AppVersion      = $"UiDesktopApp1 - {GetAssemblyVersion()}";
            Enum.TryParse(Configuration.Data.theme, out ApplicationTheme curTheme);
            CurrentTheme    = curTheme;
            Enum.TryParse(Configuration.Data.lang, out ELanguage curLang);
            CurrentLanguage = curLang;

            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
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
            }
            Configuration.Save();
        }

        [RelayCommand]
        private void OnChangeLanguage(string lang)
        {
            _localizationService.ChangeLanguage(lang);

            string ELangStr = lang.Replace('-', '_');
            Enum.TryParse(ELangStr, out ELanguage curLang);
            CurrentLanguage = curLang;

            Configuration.Data.lang = lang;
            Configuration.Save();
        }
    }
}
