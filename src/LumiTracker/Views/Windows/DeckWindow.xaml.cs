using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Input;

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
            if (_snapper != null)
            {
                Detach();
            }
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
                Expander.IsChecked = true;
                TogglePanel.Visibility = Visibility.Collapsed;
                ViewModel.MainContentHeightRatio = 1.0;
                Topmost = false;
            }
            else
            {
                TogglePanel.Visibility = Visibility.Visible;
                ViewModel.MainContentHeightRatio = Configuration.Get<double>("deck_window_height_ratio");
                Topmost = true;
            }
        }

        private void OnTabControlLoaded(object sender, RoutedEventArgs e)
        {
            DeckWindowTabControl.SelectedItem = MyDeckTab;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            TogglePanel.Background = (Brush)Resources["TogglePanelBackgroundCheckedPointerOver"];
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            TogglePanel.Background = (Brush)Resources["TogglePanelBackgroundChecked"];
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            TogglePanel.Background = (Brush)Resources["TogglePanelBackgroundCheckedPressed"];
            if (e.Source != Expander)
            {
                Expander.IsChecked = !Expander.IsChecked;
            }
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            TogglePanel.Background = (Brush)Resources["TogglePanelBackgroundCheckedPointerOver"];
        }
    }
}
