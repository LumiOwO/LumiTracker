using LumiTracker.Config;
using LumiTracker.Services;
using System.Globalization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LumiTracker.OB.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private LocalizationTextItem[] _languageNames = new LocalizationTextItem[(int)ELanguage.NumELanguages];

        [ObservableProperty]
        private int _selectedLanguageIndex = -1;

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
            // languages
            for (ELanguage i = 0; i < ELanguage.NumELanguages; i++)
            {
                var textItem = new LocalizationTextItem();
                if (i == ELanguage.FollowSystem)
                {
                    string lang = EnumHelpers.ParseLanguageName(null);
                    textItem.Text = Lang.ResourceManager.GetString("FollowSystem", new CultureInfo(lang))!;
                }
                else
                {
                    textItem.Text = EnumHelpers.GetLanguageUtf8Name(i);
                }
                LanguageNames[(int)i] = textItem;
            }
            if (Configuration.IsLanguageFollowSystem())
            {
                SelectedLanguageIndex = (int)ELanguage.FollowSystem;
            }
            else
            {
                SelectedLanguageIndex = (int)Configuration.GetELanguage();
            }

            CurrentClosingBehavior = Configuration.Get<EClosingBehavior>("closing_behavior");
            CurrentTheme           = Configuration.Get<ApplicationTheme>("theme");

            _isInitialized = true;
        }

        [RelayCommand]
        private void OnChangeLanguage()
        {
            ELanguage SelectedClientType = (ELanguage)SelectedLanguageIndex;
            string lang = SelectedClientType.ToLanguageName();

            Configuration.Set("lang", lang);
            _localizationService.ChangeLanguage(lang);
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
        public async Task OnUpdateButtonClicked()
        {
            await _updateService.TryUpdateAsync(UpdateContext);
        }

        [ObservableProperty]
        private bool _checkUpdatesOnStartup = Configuration.Get<bool>("check_updates_on_startup");

        partial void OnCheckUpdatesOnStartupChanged(bool oldValue, bool newValue)
        {
            Configuration.Set("check_updates_on_startup", newValue);
        }

        [ObservableProperty]
        private bool _checkSubscribeToBetaUpdates = Configuration.Get<bool>("subscribe_to_beta_updates");

        partial void OnCheckSubscribeToBetaUpdatesChanged(bool oldValue, bool newValue)
        {
            Configuration.Set("subscribe_to_beta_updates", newValue);
            Configuration.RemoveTemporal("releaseMeta");
        }
    }
}
