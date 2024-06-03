using LumiTracker.Helpers;
using LumiTracker.Services;
using LumiTracker.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LumiTracker.Views.Windows
{
    public interface IDeckWindow
    {
        void ShowWindow();
        void HideWindow();
        void AttachTo(IntPtr hwnd);
        void Detach();
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
            _snapper = new WindowSnapper(this, hwnd);
            _snapper.Attach();
        }
        public void Detach()
        {
            _snapper?.Detach();
            _snapper = null;
        }

        public void ShowWindow()
        {
            Show();
            ViewModel.IsShowing = true;
        }
        public void HideWindow()
        {
            Hide();
            ViewModel.IsShowing = false;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (TabItem tabItem in DeckWindowTabControl.Items)
            {
                if (tabItem.IsSelected)
                {
                    tabItem.Opacity = 1;
                }
                else
                {
                    tabItem.Opacity = 0.3;
                }
            }
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
