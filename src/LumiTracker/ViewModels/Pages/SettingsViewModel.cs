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

        private readonly UpdateService _updateService;

        public SettingsViewModel(ILocalizationService localizationService, UpdateService updateService)
        {
            _localizationService = localizationService;
            _updateService = updateService;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() { }

        private void InitializeViewModel()
        {
            Enum.TryParse(Configuration.Get<string>("lang").Replace('-', '_'), out ELanguage curLang);
            CurrentLanguage        = curLang;
            CurrentClosingBehavior = Configuration.Get<EClosingBehavior>("closing_behavior");
            CurrentTheme           = Configuration.Get<ApplicationTheme>("theme");

            _isInitialized = true;
        }

        [RelayCommand]
        private void OnChangeLanguage(string lang)
        {
            Enum.TryParse(lang.Replace('-', '_'), out ELanguage curLang);
            CurrentLanguage = curLang;
            
            _localizationService.ChangeLanguage(lang);

            Configuration.Set("lang", lang);
        }

        [RelayCommand]
        private void OnChangeClosingBehavior(string closing_behavior)
        {
            Enum.TryParse(closing_behavior, out EClosingBehavior curBehavior);
            CurrentClosingBehavior = curBehavior;

            Configuration.Set("closing_behavior", closing_behavior);
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
                    Configuration.Set("theme", "Light");

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    Configuration.Set("theme", "Dark");

                    break;

                // TODO: Follow system
                //SystemThemeWatcher.Watch(this);

            }
        }

        [ObservableProperty]
        private UpdateContext _updateContext = new ();

        [RelayCommand]
        private async Task OnUpdateButtonClicked()
        {
            await _updateService.TryUpdateAsync(UpdateContext);
        }
    }
}
