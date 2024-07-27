using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;

namespace LumiTracker.ViewModels.Windows
{
    public partial class DeckWindowViewModel : ObservableObject
    {
        // ui
        [ObservableProperty]
        private double _mainContentHeightRatio = 0.0;

        [ObservableProperty]
        private ScrollBarVisibility _VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

        [ObservableProperty]
        private SymbolRegular _toggleButtonIcon = SymbolRegular.ChevronUp48;

        [ObservableProperty]
        private bool _isShowing = false;

        // data
        [ObservableProperty]
        private CardList _myActionCardsPlayed = new ();

        [ObservableProperty]
        private CardList _opActionCardsPlayed = new ();

        [ObservableProperty]
        private DeckModel? _deckModel;

        // controls
        [ObservableProperty]
        private bool _gameStarted = false;

        [ObservableProperty]
        private bool _gameNotStarted = true;

        [ObservableProperty]
        private int _round = 0;

        private GameWatcher _gameWatcher;

        public DeckWindowViewModel(GameWatcher gameWatcher)
        {
            _gameWatcher = gameWatcher;

            _gameWatcher.GameStarted        += OnGameStarted;
            _gameWatcher.MyActionCardPlayed += OnMyActionCardPlayed;
            _gameWatcher.OpActionCardPlayed += OnOpActionCardPlayed;
            _gameWatcher.GameOver           += OnGameOver;
            _gameWatcher.RoundDetected      += OnRoundDetected;
            _gameWatcher.UnsupportedRatio   += OnUnsupportedRatio;

            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            _deckModel = new DeckModel("GLGxC4wQGMHRDI4QGNHxDZAQGeERDpIQGfExD5QRGQFREJYREBGREQkRECGhEgoREDAA");

            ResetRecordedData();
        }

        private void ResetRecordedData()
        {
            MyActionCardsPlayed = new CardList(CardList.SortType.TimestampDescending);
            OpActionCardsPlayed = new CardList(CardList.SortType.TimestampDescending);
            Round = 0;
        }

        private void UpdatePlayedActionCard(int card_id, bool is_op)
        {
            CardList ActionCardsPlayed = is_op ? OpActionCardsPlayed : MyActionCardsPlayed;
            ActionCardsPlayed.Add(card_id);
        }

        private void OnGameStarted()
        {
            ResetRecordedData();
            GameStarted = true;
            GameNotStarted = !GameStarted;
        }

        private void OnMyActionCardPlayed(int card_id)
        {
            UpdatePlayedActionCard(card_id, is_op: false);
        }

        private void OnOpActionCardPlayed(int card_id)
        {
            UpdatePlayedActionCard(card_id, is_op: true);
        }

        private void OnGameOver()
        {
            ResetRecordedData();
            GameStarted = false;
            GameNotStarted = !GameStarted;
        }

        private void OnRoundDetected(int round)
        {
            Round = round;
        }

        private void OnUnsupportedRatio()
        {
            System.Windows.MessageBox.Show(
                $"{LocalizationSource.Instance["UnsupportedRatioWarning"]}\n{LocalizationSource.Instance["SupportedRatioInfo"]}", 
                $"{LocalizationSource.Instance["AppName"]}", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        private void OnWindowWatcherExit()
        {
            ResetRecordedData();
            GameStarted = false;
            GameNotStarted = !GameStarted;
        }

    }
}
