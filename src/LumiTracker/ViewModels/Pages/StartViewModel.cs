using LumiTracker.Views.Windows;
using LumiTracker.Config;
using LumiTracker.Models;
using Microsoft.Extensions.Logging;
using System.Windows.Media;
using System.Windows.Data;
using LumiTracker.Services;
using System.Diagnostics;

namespace LumiTracker.ViewModels.Pages
{
    public partial class StartViewModel : ObservableObject
    {
        private bool Inited { get; set; } = false;

        private DeckWindow _deckWindow;

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
        private float _FPS = 0.0f;

        [ObservableProperty]
        private string _FPSText = "";

        [ObservableProperty]
        private Brush _FPSBrush = Brushes.LimeGreen;

        [ObservableProperty]
        private Visibility _FPSVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private bool _isGenshinWindowMinimized = false;

        [ObservableProperty]
        private string[] _captureTypes = new string[(int)ECaptureType.NumCaptureTypes];

        [ObservableProperty]
        private int _selectedCaptureIndex = -1;

        [ObservableProperty]
        private Visibility _captureTestButtonVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private LocalizationTextItem _cloudGameHint = new ();

        [ObservableProperty]
        private LocalizationTextItem _cloudGameHint_Web = new();

        [ObservableProperty]
        private bool _captureSelectionEnabled = true;

        [ObservableProperty]
        private bool _showUIOutside = false;

        [ObservableProperty]
        private double _deckWindowHeightRatio = 1.0;

        partial void OnSelectedClientIndexChanged(int oldValue, int newValue)
        {
            if (newValue == (int)EClientType.CloudPC)
            {
                var binding = LocalizationExtension.Create("CaptureType_CloudGameHint");
                BindingOperations.SetBinding(CloudGameHint, LocalizationTextItem.TextProperty, binding);
                BindingOperations.ClearBinding(CloudGameHint_Web, LocalizationTextItem.TextProperty);
                CloudGameHint_Web.Text = "";
                SelectedCaptureIndex = (int)ECaptureType.WindowsCapture;
                CaptureSelectionEnabled = false;
            }
            else if (newValue == (int)EClientType.CloudWeb)
            {
                var binding = LocalizationExtension.Create("CaptureType_CloudGameHint");
                BindingOperations.SetBinding(CloudGameHint, LocalizationTextItem.TextProperty, binding);
                binding = LocalizationExtension.Create("CaptureType_CloudGameHint_Web");
                BindingOperations.SetBinding(CloudGameHint_Web, LocalizationTextItem.TextProperty, binding);
                SelectedCaptureIndex = (int)ECaptureType.WindowsCapture;
                CaptureSelectionEnabled = false;
            }
            else
            {
                BindingOperations.ClearBinding(CloudGameHint, LocalizationTextItem.TextProperty);
                CloudGameHint.Text = "";
                BindingOperations.ClearBinding(CloudGameHint_Web, LocalizationTextItem.TextProperty);
                CloudGameHint_Web.Text = "";
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
                IsGenshinWindowMinimized = false;
                GameWatcherStateBrush = Brushes.DarkGray;
                FPSVisibility = CaptureTestButtonVisibility = Visibility.Collapsed;
            }
            else if (newValue == EGameWatcherState.StartingWindowWatcher)
            {
                GameWatcherStateBrush = Brushes.DarkOrange;
                FPSVisibility = CaptureTestButtonVisibility = Visibility.Collapsed;
            }
            else if (newValue == EGameWatcherState.WindowWatcherStarted)
            {
                GameWatcherStateBrush = Brushes.LimeGreen;
                FPSVisibility = CaptureTestButtonVisibility = Visibility.Visible;
                FPS = -1;
            }
            else
            {
                GameWatcherStateBrush = Brushes.DarkGray;
                FPSVisibility = CaptureTestButtonVisibility = Visibility.Collapsed;
            }
        }

        partial void OnFPSChanged(float oldValue, float newValue)
        {
            if (newValue < 20)
            {
                FPSBrush = Brushes.Red;
            }
            else if (newValue < 30)
            {
                FPSBrush = Brushes.Yellow;
            }
            else
            {
                FPSBrush = Brushes.LimeGreen;
            }

            if (newValue < 0)
            {
                FPSText = "FPS : -";
            }
            else
            {
                FPSText = $"FPS : {newValue:F1}";
            }
        }

        partial void OnIsGenshinWindowMinimizedChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                FPS = 0.0f;
            }
        }

        public StartViewModel(IDeckWindow deckWindow, GameWatcher gameWatcher, StyledContentDialogService contentDialogService)
        {
            _deckWindow  = (deckWindow as DeckWindow)!;
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

            Configuration.Logger.LogInformation($"ClientType = {ClientType.ToString()}, CaptureType = {CaptureType.ToString()}");

            // Game Watcher State
            GameWatcherState = EGameWatcherState.NoWindowFound;

            ShowUIOutside = Configuration.Get<bool>("show_ui_outside");
            DeckWindowHeightRatio = Configuration.Get<double>("deck_window_height_ratio");
            _deckWindow.SetLocation(ShowUIOutside, DeckWindowHeightRatio);

            // Start game watcher
            _gameWatcher.GenshinWindowFound += OnGenshinWindowFound;
            _gameWatcher.WindowWatcherStart += OnWindowWatcherStart;
            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;
            _gameWatcher.CaptureTestDone    += OnCaptureTestDone;
            _gameWatcher.LogFPS             += OnLogFPS;
            _gameWatcher.UnsupportedRatio   += OnUnsupportedRatio;

            _gameWatcher.Start(ClientType, CaptureType);

            Inited = true;
        }

        private void OnGenshinWindowFound()
        {
            GameWatcherState = EGameWatcherState.StartingWindowWatcher;
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            GameWatcherState = EGameWatcherState.WindowWatcherStarted;

            _deckWindow.ShowWindow();
            _deckWindow.AttachTo(hwnd);
            _deckWindow.Snapper!.GenshinWindowResized += OnGenshinWindowResized;
        }

        private void OnWindowWatcherExit()
        {
            GameWatcherState = EGameWatcherState.NoWindowFound;

            _deckWindow.Snapper!.GenshinWindowResized -= OnGenshinWindowResized;
            _deckWindow.Detach();
            _deckWindow.HideWindow();
        }

        private void OnGenshinWindowResized(int width, int height, bool isMinimized, float dpiScale)
        {
            IsGenshinWindowMinimized = isMinimized;
        }

        [RelayCommand]
        public void OnSelectedClientChanged()
        {
            EClientType  SelectedClientType  = (EClientType)SelectedClientIndex;
            ECaptureType SelectedCaptureType = (ECaptureType)SelectedCaptureIndex;
            Configuration.Logger.LogInformation($"ClientType = {SelectedClientType.ToString()}, CaptureType = {SelectedCaptureType.ToString()}");

            GameWatcherState = EGameWatcherState.NoWindowFound;

            Configuration.Set("client_type", SelectedClientType.ToString(), auto_save: false);
            if (!EnumHelpers.BitBltUnavailable(SelectedClientType))
            {
                Configuration.Set("capture_type", SelectedCaptureType.ToString(), auto_save: false);
            }
            Configuration.Save();

            _gameWatcher.ChangeGameClient(SelectedClientType, SelectedCaptureType);
        }

        [RelayCommand]
        public void OnChangeUIOutside(string newValue)
        {
            ShowUIOutside = (newValue == "1");
        }

        partial void OnShowUIOutsideChanged(bool oldValue, bool newValue)
        {
            if (!Inited) return;
            Configuration.Set("show_ui_outside", newValue);
            _deckWindow.SetLocation(ShowUIOutside, DeckWindowHeightRatio);
        }

        partial void OnDeckWindowHeightRatioChanged(double oldValue, double newValue)
        {
            if (!Inited) return;
            Configuration.Set("deck_window_height_ratio", newValue);
            _deckWindow.SetLocation(ShowUIOutside, DeckWindowHeightRatio);
        }

        [RelayCommand]
        public async Task OnCaptureTest()
        {
            if (IsGenshinWindowMinimized)
                return;

            await _gameWatcher.DumpToBackend(new
            {
                input_type = EInputType.CAPTURE_TEST.ToString(),
            });
        }

        public void OnCaptureTestDone(string filename, int width, int height)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Task task = _contentDialogService.ShowCaptureTestDialogAsync(filename, width, height);
            });
        }

        public void OnLogFPS(float fps)
        {
            FPS = fps;
        }

        private void OnUnsupportedRatio()
        {
            EClientType? clientType = _gameWatcher.ClientType;
            Debug.Assert(clientType != null);
            if (EnumHelpers.ShouldShowUnsupportedRatioWarning(clientType.Value))
            {
                System.Windows.MessageBox.Show(
                    $"{Lang.UnsupportedRatioWarning}\n{Lang.SupportedRatioInfo}",
                    $"{Lang.AppName}",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
