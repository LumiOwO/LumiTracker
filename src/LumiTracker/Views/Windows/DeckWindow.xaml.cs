using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;

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

        public CanvasWindow CanvasWindow { get; }

        public DeckWindow(DeckWindowViewModel viewModel, ICanvasWindow canvasWindow)
        {
            Loaded += DeckWindow_Loaded;

            ViewModel     = viewModel;
            CanvasWindow  = (canvasWindow as CanvasWindow)!;
            CanvasWindow.DeckWindow = this;

            ShowActivated = false;
            DataContext   = this;

            InitializeComponent();

            // Toggle Button
            Resources["ToggleButtonBackgroundChecked"]            = new SolidColorBrush(Color.FromArgb(0xff, 0x1c, 0xdd, 0xe9));
            Resources["ToggleButtonForegroundCheckedPointerOver"] = new SolidColorBrush(Color.FromArgb(0xff, 0x3d, 0xf3, 0xff));
            Resources["ToggleButtonBackgroundCheckedPressed"]     = new SolidColorBrush(Color.FromArgb(0xff, 0x5c, 0xf5, 0xff));
            // Button
            Resources["AccentButtonBackground"]                   = new SolidColorBrush(Color.FromArgb(0xff, 0x1c, 0xdd, 0xe9));
            Resources["AccentButtonBackgroundPointerOver"]        = new SolidColorBrush(Color.FromArgb(0xff, 0x3d, 0xf3, 0xff));
            Resources["AccentButtonBackgroundPressed"]            = new SolidColorBrush(Color.FromArgb(0xff, 0x5c, 0xf5, 0xff));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        private void DeckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            WindowSnapper.SetLayeredWindow(hwnd);
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

        public WindowSnapper? Snapper
        {
            get
            {
                return _snapper;
            }
        }

        public void ShowWindow()
        {
            if (!ViewModel.IsShowing)
            {
                Show();
                MainContent.Visibility = Visibility.Visible;
                ViewModel.IsShowing = true;
                CanvasWindow.ShowWindow();
            }
        }

        public void HideWindow()
        {
            if (ViewModel.IsShowing)
            {
                //Hide();
                MainContent.Visibility = Visibility.Hidden;
                ViewModel.IsShowing = false;
                CanvasWindow.HideWindow();
            }
        }

        public void OnGenshinWindowResized(int width, int height, bool isMinimized, float dpiScale)
        {
            CanvasWindow?.OnGenshinWindowResized(width, height, isMinimized, dpiScale);
        }

        public void SetbOutside(bool bOutside)
        {
            _snapper?.SetbOutside(bOutside);
            if (bOutside)
            {
                ViewModel.IsChecked = true;
                toggle.Visibility = Visibility.Collapsed;
                ViewModel.MainContentHeightRatio = 1.0;
                Topmost = false;
            }
            else
            {
                toggle.Visibility = Visibility.Visible;
                ViewModel.MainContentHeightRatio = 0.45;
                Topmost = true;
            }
        }

        private void OnTabControlLoaded(object sender, RoutedEventArgs e)
        {
            DeckWindowTabControl.SelectedItem = MyDeckTab;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsChecked = true;
        }

        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsChecked = false;
        }
    }
}
