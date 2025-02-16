using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Microsoft.Extensions.Logging;
using System.Security.Principal;

namespace LumiTracker.ViewModels.Pages
{
    public enum EOBConnectState : int
    {
        None,
        Connecting,
        Connected,
        Failed,
        Reconnecting,
    }

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

        private readonly StyledContentDialogService _contentDialogService;

        private GameWatcher _gameWatcher;

        // OB setting prompts
        [ObservableProperty]
        private string _OBHost = "";

        [ObservableProperty]
        private int _OBPort = 25251;

        [ObservableProperty]
        private LocalizationTextItem _OBConnectStateText = new ();

        [ObservableProperty]
        private Brush _OBConnectColor = Brushes.White;

        [ObservableProperty]
        private SymbolRegular _OBConnectIcon = SymbolRegular.Question24;

        [ObservableProperty]
        private Visibility _OBConnectShowLoading = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility _OBConnectShowIcon = Visibility.Collapsed;

        [ObservableProperty]
        private bool _OBConnectButtonEnabled = true;

        [ObservableProperty]
        private EOBConnectState _connectState = EOBConnectState.None;

        partial void OnConnectStateChanged(EOBConnectState oldValue, EOBConnectState newValue)
        {
            if (newValue != EOBConnectState.None)
            {
                var binding = LocalizationExtension.Create(newValue);
                BindingOperations.SetBinding(OBConnectStateText, LocalizationTextItem.TextProperty, binding);
            }
            else
            {
                BindingOperations.ClearBinding(OBConnectStateText, LocalizationTextItem.TextProperty);
                OBConnectStateText.Text = "";
            }

            if (newValue == EOBConnectState.Connecting)
            {
                OBConnectColor = Brushes.Gray;
                OBConnectShowLoading = Visibility.Visible;
                OBConnectShowIcon = Visibility.Collapsed;
            }
            else if (newValue == EOBConnectState.Connected)
            {
                OBConnectColor = Brushes.Green;
                OBConnectIcon = SymbolRegular.Checkmark24;
                OBConnectShowLoading = Visibility.Collapsed;
                OBConnectShowIcon = Visibility.Visible;
            }
            else if (newValue == EOBConnectState.Failed)
            {
                OBConnectColor = Brushes.Red;
                OBConnectIcon = SymbolRegular.Dismiss24;
                OBConnectShowLoading = Visibility.Collapsed;
                OBConnectShowIcon = Visibility.Visible;
            }
            else if (newValue == EOBConnectState.Reconnecting)
            {
                OBConnectColor = Brushes.Gray;
                OBConnectShowLoading = Visibility.Visible;
                OBConnectShowIcon = Visibility.Collapsed;
            }
            else if (newValue == EOBConnectState.None)
            {
                OBConnectShowLoading = Visibility.Collapsed;
                OBConnectShowIcon = Visibility.Collapsed;
            }
        }

        partial void OnOBConnectShowLoadingChanged(Visibility oldValue, Visibility newValue)
        {
            OBConnectButtonEnabled = (newValue != Visibility.Visible);
        }

        public SettingsViewModel(ILocalizationService localizationService, UpdateService updateService, GameWatcher gameWatcher, StyledContentDialogService contentDialogService)
        {
            _localizationService = localizationService;
            _updateService = updateService;
            _gameWatcher = gameWatcher;
            _contentDialogService = contentDialogService;
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
            OBHost                 = Configuration.Get<string>("ob_host");
            OBPort                 = Configuration.Get<int>("ob_port");

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

                    ApplicationThemeService.ChangeThemeTo(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;
                    Configuration.Set("theme", "Light");

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeService.ChangeThemeTo(ApplicationTheme.Dark);
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

        [ObservableProperty]
        private bool _runAsAdmin = IsRunningAsAdmin();

        partial void OnRunAsAdminChanged(bool oldValue, bool newValue)
        {
            Configuration.Set("run_as_admin", newValue);
            if (!IsRunningAsAdmin() && newValue) 
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Task task = _contentDialogService.ShowRestartDialogAsync();
                });
            }
        }

        private static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        partial void OnOBHostChanged(string? oldValue, string newValue)
        {
            Configuration.Set("ob_host", newValue);
        }

        partial void OnOBPortChanged(int oldValue, int newValue)
        {
            Configuration.Set("ob_port", newValue);
        }

        [RelayCommand]
        public async Task OnConnectToOBServer(EOBConnectState state)
        {
            if (ConnectState == EOBConnectState.Connected)
            {
                return;
            }
            ConnectState = state;

            bool success = await _gameWatcher.ConnectToServer(OBHost, OBPort);
            if (success)
            {
                ConnectState = EOBConnectState.Connected;
                _gameWatcher.AddServerDisconnectedCallback(OnDisconnected);
            }
            else if (state != EOBConnectState.Reconnecting)
            {
                ConnectState = EOBConnectState.Failed;
            }
        }

        public void OnDisconnected()
        {
            if (ConnectState == EOBConnectState.None) return;

            Application.Current.Dispatcher.Invoke(async () =>
            {
                ConnectState = EOBConnectState.Failed;
                // Try to reconnect
                do
                {
                    await Task.Delay(500);
                    Configuration.Logger.LogError("Server disconnected, try to reconnect...");
                    await OnConnectToOBServer(EOBConnectState.Reconnecting);
                } while (ConnectState != EOBConnectState.Connected && ConnectState != EOBConnectState.None);
            });
        }
    }
}
