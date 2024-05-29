using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.ViewModels.Pages;
using LumiTracker.Views.Pages;
using Wpf.Ui.Appearance;
using Wpf.Ui;

namespace LumiTracker.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = Lang.AppName;

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.StartPage)
            },
            new NavigationViewItem()
            {
                Content = "Data",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
                TargetPageType = typeof(Views.Pages.AboutPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "A" },
            new MenuItem { Header = "Quit", Tag = "B" },
        };

        private readonly IServiceProvider _serviceProvider;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Init()
        {
            var cfg = Configuration.Data;
            ILocalizationService localizationService = (
                _serviceProvider.GetService(typeof(ILocalizationService)) as ILocalizationService
            )!;
            localizationService.ChangeLanguage(cfg.lang);

            Enum.TryParse(cfg.theme, out ApplicationTheme curTheme);
            ApplicationThemeManager.Apply(curTheme);


            StartViewModel startViewModel = (
                _serviceProvider.GetService(typeof(StartViewModel)) as StartViewModel
            )!;
            startViewModel.Init();
        }
    }
}
