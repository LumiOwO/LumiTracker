using LumiTracker.Views.Windows;
using LumiTracker.Watcher;
using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;
using System;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using System.Reflection.Metadata;
using LumiTracker.Helpers;

namespace LumiTracker.ViewModels.Pages
{
    public partial class StartViewModel : ObservableObject
    {
        private IDeckWindow _deckWindow;
        private WindowSnapper? _snapper;


        private GameWatcher _gameWatcher;

        [ObservableProperty]
        private EClientType _currentClientType;

        [ObservableProperty]
        private IEnumerable<EClientType> _clientTypes = Enum.GetValues(typeof(EClientType)).Cast<EClientType>();

        [ObservableProperty]
        private EGameWatcherState _gameWatcherState = EGameWatcherState.NoWindowFound;

        [ObservableProperty]
        private Brush _gameWatcherStateBrush = Brushes.DarkGray;

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher)
        {
            _deckWindow  = deckWindow;
            _gameWatcher = gameWatcher;
        }

        public void Init()
        {
            Enum.TryParse(Configuration.Data.client_type, out EClientType clientType);
            CurrentClientType = clientType;

            _gameWatcher.GenshinWindowFound += OnGenshinWindowFound;
            _gameWatcher.WindowWatcherStart += OnWindowWatcherStart;
            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            _gameWatcher.Start(GetProcessName(clientType));
        }

        private string GetProcessName(EClientType clientType)
        {
            string processName = "";
            if (clientType == EClientType.YuanShen)
            {
                processName = "YuanShen.exe";
            }
            else if (clientType == EClientType.Global)
            {
                processName = "GenshinImpact.exe";
            }
            else if (clientType == EClientType.Cloud)
            {
                processName = "Genshin Impact Cloud Game.exe";
            }
            return processName;
        }

        private void OnGenshinWindowFound()
        {
            GameWatcherState = EGameWatcherState.WindowNotForeground;
            GameWatcherStateBrush = Brushes.DarkOrange;
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            GameWatcherState = EGameWatcherState.WindowWatcherStarted;
            GameWatcherStateBrush = Brushes.LimeGreen;

            _snapper = new WindowSnapper((Window)_deckWindow, hwnd);
            _snapper.Attach();
            _deckWindow.ShowWindow();
        }

        private void OnWindowWatcherExit()
        {
            _deckWindow.HideWindow();
            _snapper?.Detach();
            _snapper = null;
        }

        [RelayCommand]
        public void OnSelectedClientChanged(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null)
            {
                Configuration.Logger.LogDebug($"Trigger an invalid OnSelectedClientChanged for language change");
                return;
            }

            EClientType SelectedClientType = (EClientType)comboBox.SelectedItem;
            Configuration.Logger.LogDebug($"{SelectedClientType}");
            if (SelectedClientType == CurrentClientType) 
            { 
                return; 
            }
            CurrentClientType = SelectedClientType;

            GameWatcherState = EGameWatcherState.NoWindowFound;
            GameWatcherStateBrush = Brushes.DarkGray;

            string processName = GetProcessName(CurrentClientType);
            Configuration.Logger.LogDebug(processName);

            Configuration.Data.client_type = CurrentClientType.ToString();
            Configuration.Save();

            _gameWatcher.ChangeGameClient(processName);
        }
    }
}
