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

namespace LumiTracker.ViewModels.Pages
{
    public partial class StartViewModel : ObservableObject
    {
        private IDeckWindow _deckWindow;

        private GameWatcher _gameWatcher;

        [ObservableProperty]
        private EClientType _currentClientType;

        [ObservableProperty]
        public IEnumerable<EClientType> _clientTypes = Enum.GetValues(typeof(EClientType)).Cast<EClientType>();

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher)
        {
            _deckWindow  = deckWindow;
            _gameWatcher = gameWatcher;
        }

        public void Init()
        {
            Enum.TryParse(Configuration.Data.client_type, out EClientType clientType);
            CurrentClientType = clientType;

            _gameWatcher.GenshinWindowFound += () => _deckWindow.ShowWindow();
            _gameWatcher.WindowWatcherExit  += () => _deckWindow.HideWindow();

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

        [RelayCommand]
        public void OnSelectedClientChanged()
        {
            string processName = GetProcessName(CurrentClientType);
            Configuration.Logger.LogDebug(processName);

            Configuration.Data.client_type = CurrentClientType.ToString();
            Configuration.Save();
        }
    }
}
