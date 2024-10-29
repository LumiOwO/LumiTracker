using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using LumiTracker.Controls;
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
            HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;

            MARGINS margins = new MARGINS() { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Set the window to be layered, so it can be captured by OBS with AllowsTransparency="True"
            // Note: OBS must use Windows Capture to capture this window
            const int GWL_EXSTYLE = -20;
            const int WS_EX_LAYERED = 0x80000;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
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
            ViewModel.IsChecked = true;
        }

        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsChecked = false;
        }
    }
}
