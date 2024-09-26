using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using LumiTracker.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

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

        public event OnGenshinWindowResizedCallback? GenshinWindowResized;

        public DeckWindowViewModel ViewModel { get; }

        public DeckWindow(DeckWindowViewModel viewModel)
        {
            Loaded += DeckWindow_Loaded;

            ShowActivated = false;
            ViewModel     = viewModel;
            DataContext   = this;

            InitializeComponent();
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        private void DeckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;

            MARGINS margins = new MARGINS() { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
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

        public void OnGenshinWindowResized(int width, int height, bool isMinimized)
        {
            Configuration.Logger.LogDebug($"[DeckWindow] OnGenshinWindowResized: {width} x {height}, isMinimized = {isMinimized}");
            GenshinWindowResized?.Invoke(width, height, isMinimized);
        }

        public void ShowWindow()
        {
            if (!ViewModel.IsShowing)
            {
                Show();
                MainContent.Visibility = Visibility.Visible;
                ViewModel.IsShowing = true;
            }
        }
        public void HideWindow()
        {
            if (ViewModel.IsShowing)
            {
                //Hide();
                MainContent.Visibility = Visibility.Hidden;
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
