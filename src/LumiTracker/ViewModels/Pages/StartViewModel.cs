using LumiTracker.Views.Windows;
using LumiTracker.Watcher;
using LumiTracker.Config;
using LumiTracker.Models;

namespace LumiTracker.ViewModels.Pages
{
    public partial class StartViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _counter = 0;

        private IDeckWindow _deckWindow;

        private GameWatcher _gameWatcher;

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher)
        {
            _deckWindow  = deckWindow;
            _gameWatcher = gameWatcher;
        }

        [RelayCommand]
        private void OnCounterIncrement()
        {
            Counter++;
            if ((Counter & 0x1) != 0)
            {
                _deckWindow.ShowWindow();
            }
            else
            {
                _deckWindow.HideWindow();
            }
        }

        public void Init()
        {
            _gameWatcher.GenshinWindowFound += () => _deckWindow.ShowWindow();
            _gameWatcher.WindowWatcherExit  += () => _deckWindow.HideWindow();

            _gameWatcher.Start();
        }
    }
}
