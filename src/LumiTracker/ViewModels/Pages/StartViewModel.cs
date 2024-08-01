using LumiTracker.Views.Windows;
using LumiTracker.Config;
using LumiTracker.Models;
using Microsoft.Extensions.Logging;
using System.Windows.Media;
using System.Windows.Data;
using LumiTracker.Services;

namespace LumiTracker.ViewModels.Pages
{
    public partial class StartViewModel : ObservableObject
    {
        private IDeckWindow _deckWindow;

        private GameWatcher _gameWatcher;

        [ObservableProperty]
        private LocalizationTextItem[] _clientTypes = new LocalizationTextItem[(int)EClientType.NumClientTypes];

        [ObservableProperty]
        private int _selectedClientIndex = -1;

        [ObservableProperty]
        private EGameWatcherState _gameWatcherState = EGameWatcherState.Invalid;

        [ObservableProperty]
        private LocalizationTextItem _gameWatcherStateText = new ();
        partial void OnGameWatcherStateChanged(EGameWatcherState oldValue, EGameWatcherState newValue)
        {
            var binding = LocalizationExtension.Create(newValue);
            BindingOperations.SetBinding(GameWatcherStateText, LocalizationTextItem.TextProperty, binding);
        }

        [ObservableProperty]
        private Brush _gameWatcherStateBrush = Brushes.DarkGray;

        [ObservableProperty]
        private bool _showUIOutside = false;

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher)
        {
            _deckWindow  = deckWindow;
            _gameWatcher = gameWatcher;
        }

        public void Init()
        {
            // Client types
            for (EClientType i = 0; i < EClientType.NumClientTypes; i++)
            {
                var textItem = new LocalizationTextItem();
                var binding  = LocalizationExtension.Create(i);
                BindingOperations.SetBinding(textItem, LocalizationTextItem.TextProperty, binding);
                ClientTypes[(int)i] = textItem;
            }
            EClientType ClientType = Configuration.Get<EClientType>("client_type");
            SelectedClientIndex = (int)ClientType;

            // Game Watcher State
            GameWatcherState = EGameWatcherState.NoWindowFound;

            // Options in main page
            ShowUIOutside = Configuration.Get<bool>("show_ui_outside");
            _deckWindow.SetbOutside(ShowUIOutside);

            // Start game watcher
            _gameWatcher.GenshinWindowFound += OnGenshinWindowFound;
            _gameWatcher.WindowWatcherStart += OnWindowWatcherStart;
            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            _gameWatcher.Start(GetProcessName(ClientType));
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

            _deckWindow.ShowWindow();
            _deckWindow.AttachTo(hwnd);
        }

        private void OnWindowWatcherExit()
        {
            GameWatcherState = EGameWatcherState.NoWindowFound;
            GameWatcherStateBrush = Brushes.DarkGray;

            _deckWindow.Detach();
            _deckWindow.HideWindow();
        }

        [RelayCommand]
        public void OnSelectedClientChanged()
        {
            EClientType SelectedClientType = (EClientType)SelectedClientIndex;
            Configuration.Logger.LogDebug($"{SelectedClientType}");

            GameWatcherState = EGameWatcherState.NoWindowFound;
            GameWatcherStateBrush = Brushes.DarkGray;

            string processName = GetProcessName(SelectedClientType);
            Configuration.Logger.LogDebug(processName);

            Configuration.Set("client_type", SelectedClientType.ToString());

            _gameWatcher.ChangeGameClient(processName);
        }

        [RelayCommand]
        public void OnShowUIOutsideCheckBoxToggled()
        {
            ShowUIOutside = !ShowUIOutside;
            _deckWindow.SetbOutside(ShowUIOutside);

            Configuration.Set("show_ui_outside", ShowUIOutside);
        }
    }
}
