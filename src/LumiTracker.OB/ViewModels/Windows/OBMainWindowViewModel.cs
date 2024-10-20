using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.Helpers;
using LumiTracker.OB.Views.Pages;
using System.Windows.Input;
using System.Windows.Data;

namespace LumiTracker.OB.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private LocalizationTextItem _appTitle = new ();

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
        private NavigationViewItem? _duelViewItem;

        [ObservableProperty]
        private NavigationViewItem? _settingsViewItem;

        [ObservableProperty]
        private MenuItem? _homeTrayMenuItem;
        [ObservableProperty]
        private MenuItem? _quitTrayMenuItem;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var binding = LocalizationExtension.Create("OB_AppSubTitle");
            binding.Converter = new MainWindowTitleNameConverter();
            BindingOperations.SetBinding(_appTitle, LocalizationTextItem.TextProperty, binding);
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

            DuelViewItem = new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.People24 },
                TargetPageType = typeof(DuelPage)
            };
            itemNameBinding = LocalizationExtension.Create("Duel");
            DuelViewItem.SetBinding(NavigationViewItem.ContentProperty, itemNameBinding);

            MenuItems = new () { StartViewItem, DuelViewItem };

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
            localizationService.ChangeLanguage(Configuration.GetLanguageName());
        }
    }
}
