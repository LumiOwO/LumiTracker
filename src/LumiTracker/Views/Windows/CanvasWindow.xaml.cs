using LumiTracker.Helpers;
using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using System.Windows.Interop;
using System.Windows.Data;

namespace LumiTracker.Views.Windows
{
    public interface ICanvasWindow
    {
        void ShowWindow();
        void HideWindow();
    }

    public partial class CanvasWindow : ICanvasWindow
    {
        public CanvasWindowViewModel ViewModel { get; }
        public DeckWindow? DeckWindow { get; set; }

        public CanvasWindow(CanvasWindowViewModel viewModel)
        {
            Loaded += CanvasWindow_Loaded;

            ViewModel = viewModel;

            ShowActivated = false;
            DataContext = this;

            InitializeComponent();

            // Enable thread-safe collection access
            BindingOperations.EnableCollectionSynchronization(ViewModel.Elements, new object());
        }

        private void CanvasWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            WindowSnapper.SetLayeredWindow(hwnd, true);
        }

        public void ShowWindow()
        {
            Show();
            OverlayCanvas.Visibility = Visibility.Visible;
            Owner = DeckWindow;
        }
        public void HideWindow()
        {
            //Hide();
            OverlayCanvas.Visibility = Visibility.Hidden;
        }

        public void OnGenshinWindowResized(int width, int height, bool isMinimized, float dpiScale)
        {
            if (isMinimized) return;
            ViewModel.ResizeAllElements(width, height, dpiScale);
        }
    }
}
