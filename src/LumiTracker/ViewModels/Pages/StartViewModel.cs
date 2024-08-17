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

        private readonly StyledContentDialogService _contentDialogService;

        [ObservableProperty]
        private LocalizationTextItem[] _clientTypes = new LocalizationTextItem[(int)EClientType.NumClientTypes];

        [ObservableProperty]
        private int _selectedClientIndex = -1;

        [ObservableProperty]
        private EGameWatcherState _gameWatcherState = EGameWatcherState.Invalid;

        [ObservableProperty]
        private LocalizationTextItem _gameWatcherStateText = new ();
        
        [ObservableProperty]
        private Brush _gameWatcherStateBrush = Brushes.DarkGray;

        [ObservableProperty]
        private string[] _captureTypes = new string[(int)ECaptureType.NumCaptureTypes];

        [ObservableProperty]
        private int _selectedCaptureIndex = -1;

        [ObservableProperty]
        private Visibility _captureTestButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private LocalizationTextItem _cloudGameHint = new ();

        [ObservableProperty]
        private bool _captureSelectionEnabled = true;

        [ObservableProperty]
        private bool _showUIOutside = false;

        partial void OnSelectedClientIndexChanged(int oldValue, int newValue)
        {
            if (newValue == (int)EClientType.Cloud)
            {
                var binding = LocalizationExtension.Create("CaptureType_CloudGameHint");
                BindingOperations.SetBinding(CloudGameHint, LocalizationTextItem.TextProperty, binding);
                SelectedCaptureIndex = (int)ECaptureType.WindowsCapture;
                CaptureSelectionEnabled = false;
            }
            else
            {
                BindingOperations.ClearBinding(CloudGameHint, LocalizationTextItem.TextProperty);
                CloudGameHint.Text = "";
                SelectedCaptureIndex = (int)Configuration.Get<ECaptureType>("capture_type");
                CaptureSelectionEnabled = true;
            }
        }

        partial void OnGameWatcherStateChanged(EGameWatcherState oldValue, EGameWatcherState newValue)
        {
            var binding = LocalizationExtension.Create(newValue);
            BindingOperations.SetBinding(GameWatcherStateText, LocalizationTextItem.TextProperty, binding);

            if (newValue == EGameWatcherState.NoWindowFound)
            {
                GameWatcherStateBrush = Brushes.DarkGray;
                CaptureTestButtonVisibility = Visibility.Collapsed;
            }
            else if (newValue == EGameWatcherState.WindowNotForeground)
            {
                GameWatcherStateBrush = Brushes.DarkOrange;
                CaptureTestButtonVisibility = Visibility.Collapsed;
            }
            else if (newValue == EGameWatcherState.WindowWatcherStarted)
            {
                GameWatcherStateBrush = Brushes.LimeGreen;
                CaptureTestButtonVisibility = Visibility.Visible;
            }
            else
            {
                GameWatcherStateBrush = Brushes.DarkGray;
                CaptureTestButtonVisibility = Visibility.Collapsed;
            }
        }

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher, StyledContentDialogService contentDialogService)
        {
            _deckWindow  = deckWindow;
            _gameWatcher = gameWatcher;
            _contentDialogService = contentDialogService;
        }

        public void Init()
        {
            // Capture types
            for (ECaptureType i = 0; i < ECaptureType.NumCaptureTypes; i++)
            {
                CaptureTypes[(int)i] = i.ToString();
            }
            ECaptureType CaptureType = Configuration.Get<ECaptureType>("capture_type");
            SelectedCaptureIndex = (int)CaptureType;

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

            // Show UI outside
            ShowUIOutside = Configuration.Get<bool>("show_ui_outside");
            _deckWindow.SetbOutside(ShowUIOutside);

            // Start game watcher
            _gameWatcher.GenshinWindowFound += OnGenshinWindowFound;
            _gameWatcher.WindowWatcherStart += OnWindowWatcherStart;
            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;
            _gameWatcher.CaptureTestDone    += OnCaptureTestDone;

            _gameWatcher.Start(EClientProcessNames.Values[(int)ClientType]);
        }

        private void OnGenshinWindowFound()
        {
            GameWatcherState = EGameWatcherState.WindowNotForeground;
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            GameWatcherState = EGameWatcherState.WindowWatcherStarted;

            _deckWindow.ShowWindow();
            _deckWindow.AttachTo(hwnd);
        }

        private void OnWindowWatcherExit()
        {
            GameWatcherState = EGameWatcherState.NoWindowFound;

            _deckWindow.Detach();
            _deckWindow.HideWindow();
        }

        [RelayCommand]
        public void OnSelectedClientChanged()
        {
            EClientType  SelectedClientType  = (EClientType)SelectedClientIndex;
            ECaptureType SelectedCaptureType = (ECaptureType)SelectedCaptureIndex;
            string processName = EClientProcessNames.Values[(int)SelectedClientType];
            Configuration.Logger.LogInformation($"Client = {processName}, CaptureType = {SelectedCaptureType.ToString()}");

            GameWatcherState = EGameWatcherState.NoWindowFound;

            Configuration.Set("client_type",  SelectedClientType.ToString(), auto_save: false);
            if (SelectedClientType != EClientType.Cloud)
            {
                Configuration.Set("capture_type", SelectedCaptureType.ToString(), auto_save: false);
            }
            Configuration.Save();

            _gameWatcher.ChangeGameClient(processName);
        }

        [RelayCommand]
        public void OnShowUIOutsideCheckBoxToggled()
        {
            ShowUIOutside = !ShowUIOutside;
            _deckWindow.SetbOutside(ShowUIOutside);

            Configuration.Set("show_ui_outside", ShowUIOutside);
        }

        [RelayCommand]
        public async Task OnCaptureTest()
        {
            await _gameWatcher.DumpToBackend(Configuration.LogDir);
        }

        public void OnCaptureTestDone(string filename)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Task task = _contentDialogService.ShowCaptureTestDialogAsync(filename);
            });
        }
    }
}
