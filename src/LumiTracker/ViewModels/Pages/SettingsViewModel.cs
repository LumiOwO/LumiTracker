using LumiTracker.Config;
using System.Globalization;
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
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        [RelayCommand]
        private void OnChangeLanguage(string lang)
        {
            string ELangStr = lang.Replace('-', '_');
            Enum.TryParse(ELangStr, out ELanguage curLang);
            CurrentLanguage = curLang;

            Configuration.Data.lang = lang;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
        }
    }
}
