using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.ViewModels.Pages;
using LumiTracker.Views.Pages;
using Wpf.Ui.Appearance;
using Wpf.Ui;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LumiTracker.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<object>? _menuItems;

        [ObservableProperty]
        private ObservableCollection<object>? _footerMenuItems;

        [ObservableProperty]
        private ObservableCollection<Wpf.Ui.Controls.MenuItem>? _trayMenuItems;

        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private NavigationViewItem? _startViewItem;
        [ObservableProperty]
        private NavigationViewItem? _deckViewItem;
        [ObservableProperty]
        private NavigationViewItem? _rankViewItem;
        [ObservableProperty]
        private NavigationViewItem? _aboutViewItem;

        [ObservableProperty]
        private NavigationViewItem? _settingsViewItem;

        [ObservableProperty]
        private Wpf.Ui.Controls.MenuItem? _homeTrayMenuItem;
        [ObservableProperty]
        private Wpf.Ui.Controls.MenuItem? _quitTrayMenuItem;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Init(ICommand TrayMenuItemClicked)
        {
            // init menu items
            StartViewItem = new NavigationViewItem()
            {
                Content = LocalizationSource.Instance["Start"],
                Icon = new SymbolIcon { Symbol = SymbolRegular.Power24 },
                TargetPageType = typeof(StartPage)
            };
            DeckViewItem = new NavigationViewItem()
            {
                Content = LocalizationSource.Instance["DeckPageTitle"],
                Icon = new SymbolIcon { Symbol = SymbolRegular.TextBulletListLtr24 },
                TargetPageType = typeof(DeckPage)
            };
            RankViewItem = new NavigationViewItem()
            {
                Content = LocalizationSource.Instance["Rank"],
                Icon = new SymbolIcon { Symbol = SymbolRegular.Crown20 },
                TargetPageType = typeof(RankPage)
            };
            AboutViewItem = new NavigationViewItem()
            {
                Content = LocalizationSource.Instance["About"],
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(AboutPage)
            };
            MenuItems = new() { StartViewItem, DeckViewItem, RankViewItem, AboutViewItem };

            SettingsViewItem = new NavigationViewItem()
            {
                Content = LocalizationSource.Instance["Settings"],
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(SettingsPage)
            };
            FooterMenuItems = new() { SettingsViewItem };

            HomeTrayMenuItem = new Wpf.Ui.Controls.MenuItem
            {
                Header  = LocalizationSource.Instance["Tray_Home"],
                Command = TrayMenuItemClicked,
                CommandParameter = "tray_home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home20 },
            };
            QuitTrayMenuItem = new Wpf.Ui.Controls.MenuItem
            {
                Header  = LocalizationSource.Instance["Tray_Quit"],
                Command = TrayMenuItemClicked,
                CommandParameter = "tray_quit",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 },
            };
            TrayMenuItems = new() { HomeTrayMenuItem, QuitTrayMenuItem };

            // refresh language
            ILocalizationService localizationService = (
                _serviceProvider.GetService(typeof(ILocalizationService)) as ILocalizationService
            )!;
            localizationService.ChangeLanguage(Configuration.Get<string>("lang"));

            // refresh theme
            ApplicationThemeManager.Apply(Configuration.Get<ApplicationTheme>("theme"));
        }
    }
}
