using LumiTracker.Models;
using LumiTracker.Services;
using LumiTracker.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
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
        void CloseWindow();
    }


    public partial class DeckWindow : IDeckWindow
    {
        public DeckWindowViewModel ViewModel { get; }

        private GameWatcher _gameWatcher;

        public DeckWindow(DeckWindowViewModel viewModel, GameWatcher gameWatcher)
        {
            ShowActivated = false;
            ViewModel     = viewModel;
            DataContext   = this;
            _gameWatcher  = gameWatcher;

            InitializeComponent();
        }

        public void ShowWindow() => Show();
        public void HideWindow() => Hide();

        public void CloseWindow() => Close();
    }
}
