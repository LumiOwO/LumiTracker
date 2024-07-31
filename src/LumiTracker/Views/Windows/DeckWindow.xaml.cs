using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using LumiTracker.Controls;

namespace LumiTracker.Views.Windows
{
    public interface IDeckWindow
    {
        void ShowWindow();
        void HideWindow();
        void AttachTo(IntPtr hwnd);
        void Detach();
        void SetbOutside(bool bOutside);
    }

    public partial class DeckWindow : IDeckWindow
    {
        private WindowSnapper? _snapper;

        public DeckWindowViewModel ViewModel { get; }

        public DeckWindow(DeckWindowViewModel viewModel)
        {
            ShowActivated = false;
            ViewModel     = viewModel;
            DataContext   = this;

            InitializeComponent();
        }

        public void AttachTo(IntPtr hwnd)
        {
            _snapper = new WindowSnapper(this, hwnd, Configuration.Get<bool>("show_ui_outside"));
            _snapper.Attach();
        }
        public void Detach()
        {
            _snapper?.Detach();
            _snapper = null;
        }

        public void ShowWindow()
        {
            if (!ViewModel.IsShowing)
            {
                Show();
                ViewModel.IsShowing = true;
            }
        }
        public void HideWindow()
        {
            if (ViewModel.IsShowing)
            {
                Hide();
                ViewModel.IsShowing = false;
            }
        }

        public void SetbOutside(bool bOutside)
        {
            _snapper?.SetbOutside(bOutside);
            if (bOutside)
            {
                toggle.IsChecked = true;
                toggle.Visibility = toggleIcon.Visibility = Visibility.Collapsed;
                ViewModel.MainContentHeightRatio = 1.0;
                Topmost = false;
            }
            else
            {
                toggle.Visibility = toggleIcon.Visibility = Visibility.Visible;
                ViewModel.MainContentHeightRatio = 0.45;
                Topmost = true;
            }
        }

        private void OnTabControlLoaded(object sender, RoutedEventArgs e)
        {
            DeckWindowTabControl.SelectedItem = MyDeckTab;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (TabItem tabItem in DeckWindowTabControl.Items)
            {
                var header = (tabItem.Header as DeckWindowTabHeader)!;
                if (tabItem.IsSelected)
                {
                    header.Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                }
                else
                {
                    header.Foreground = new SolidColorBrush(Color.FromArgb(200, 200, 200, 200));
                }
            }
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleButtonIcon = SymbolRegular.ChevronDown48;
        }

        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleButtonIcon = SymbolRegular.ChevronUp48;
        }
    }
}
