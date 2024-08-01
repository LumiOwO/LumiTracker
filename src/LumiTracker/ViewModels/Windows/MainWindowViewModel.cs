using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.Views.Pages;
using Wpf.Ui.Appearance;
using System.Windows.Input;
using System.Windows.Media;

namespace LumiTracker.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<object>? _menuItems;

        [ObservableProperty]
        private ObservableCollection<object>? _footerMenuItems;

        [ObservableProperty]
        private ObservableCollection<MenuItem>? _trayMenuItems;

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
        private MenuItem? _homeTrayMenuItem;
        [ObservableProperty]
        private MenuItem? _quitTrayMenuItem;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            // refresh theme
            ApplicationThemeManager.Apply(Configuration.Get<ApplicationTheme>("theme"));
            // Overwrite accent color
            ApplicationAccentColorManager.Apply(
                Color.FromArgb(0xff, 0x1c, 0xdd, 0xe9),
                ApplicationTheme.Dark
            );
        }

        public void Init(ICommand TrayMenuItemClicked)
        {
            LocalizationExtension? itemNameBinding = null;

            // init menu items
            StartViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Power24 },
                TargetPageType = typeof(StartPage)
            };
            itemNameBinding = LocalizationExtension.Create("Start");
            StartViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            DeckViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.TextBulletListLtr24 },
                TargetPageType = typeof(DeckPage)
            };
            itemNameBinding = LocalizationExtension.Create("DeckPageTitle");
            DeckViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            RankViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Crown20 },
                TargetPageType = typeof(RankPage)
            };
            itemNameBinding = LocalizationExtension.Create("Rank");
            RankViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            AboutViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(AboutPage)
            };
            itemNameBinding = LocalizationExtension.Create("About");
            AboutViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            MenuItems = new () { StartViewItem, DeckViewItem, RankViewItem, AboutViewItem };

            SettingsViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(SettingsPage)
            };
            itemNameBinding = LocalizationExtension.Create("Settings");
            SettingsViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            FooterMenuItems = new () { SettingsViewItem };

            HomeTrayMenuItem = new MenuItem
            {
                Command = TrayMenuItemClicked,
                CommandParameter = "tray_home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home20 },
            };
            itemNameBinding = LocalizationExtension.Create("Tray_Home");
            HomeTrayMenuItem.SetBinding(MenuItem.HeaderProperty, itemNameBinding);

            QuitTrayMenuItem = new MenuItem
            {
                Command = TrayMenuItemClicked,
                CommandParameter = "tray_quit",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 },
            };
            itemNameBinding = LocalizationExtension.Create("Tray_Quit");
            QuitTrayMenuItem.SetBinding(MenuItem.HeaderProperty, itemNameBinding);

            TrayMenuItems = new() { HomeTrayMenuItem, QuitTrayMenuItem };

            // refresh language
            ILocalizationService localizationService = (
                _serviceProvider.GetService(typeof(ILocalizationService)) as ILocalizationService
            )!;
            localizationService.ChangeLanguage(Configuration.Get<string>("lang"));
        }
    }
}
