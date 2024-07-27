using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;
using Swordfish.NET.Collections;

using CardList = Swordfish.NET.Collections.ConcurrentObservableSortedDictionary<
    int, LumiTracker.ViewModels.Windows.ActionCardView>;
using CardListTimestamps = System.Collections.Generic.Dictionary<
    int, System.DateTime>;

namespace LumiTracker.ViewModels.Windows
{
    public partial class ActionCardView : ObservableObject
    {
        [ObservableProperty]
        public int     _count;
        [ObservableProperty]
        public string  _cardName;
        [ObservableProperty]
        public string  _snapshotUri;

        public ActionCardView(int card_id) 
        {
            var cardInfo = Configuration.Database["actions"]![card_id]!;
            Count        = 1;
            CardName     = cardInfo[Configuration.Data.lang]!.ToString();
            SnapshotUri  = $"pack://siteoforigin:,,,/assets/snapshots/actions/{card_id}.jpg";
        }
    }

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
        private CardListTimestamps MyActionCardsPlayedTimestamps = new();

        [ObservableProperty]
        private CardList _opActionCardsPlayed = new ();
        private CardListTimestamps OpActionCardsPlayedTimestamps = new();

        [ObservableProperty]
        private bool _gameStarted = false;

        [ObservableProperty]
        private bool _gameNotStarted = true;

        [ObservableProperty]
        private int _round = 0;

        private GameWatcher _gameWatcher;

        public DeckWindowViewModel(GameWatcher gameWatcher, Deck deck)
        {
            _gameWatcher = gameWatcher;

            _gameWatcher.GameStarted        += OnGameStarted;
            _gameWatcher.MyActionCardPlayed += OnMyActionCardPlayed;
            _gameWatcher.OpActionCardPlayed += OnOpActionCardPlayed;
            _gameWatcher.GameOver           += OnGameOver;
            _gameWatcher.RoundDetected      += OnRoundDetected;
            _gameWatcher.UnsupportedRatio   += OnUnsupportedRatio;

            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            ResetRecordedData();
        }

        private CardList CreateCardList(bool is_op)
        {
            CardListTimestamps timestamps = is_op ? OpActionCardsPlayedTimestamps : MyActionCardsPlayedTimestamps;
            IComparer<int> comparer = Comparer<int>.Create((a, b) =>
            {
                DateTime a_timestamp = timestamps[a];
                DateTime b_timestamp = timestamps[b];
                int res = a_timestamp.CompareTo(b_timestamp);
                // Descending
                return -res;
            });
            return new CardList(comparer);
        }

        private void ResetRecordedData()
        {
            MyActionCardsPlayedTimestamps = new ();
            OpActionCardsPlayedTimestamps = new ();
            MyActionCardsPlayed = CreateCardList(is_op: false);
            OpActionCardsPlayed = CreateCardList(is_op: true);
            Round = 0;
        }

        private void UpdatePlayedActionCard(int card_id, bool is_op)
        {
            CardList ActionCardsPlayed = is_op ? OpActionCardsPlayed : MyActionCardsPlayed;
            CardListTimestamps timestamps = is_op ? OpActionCardsPlayedTimestamps : MyActionCardsPlayedTimestamps;

            if (ActionCardsPlayed.TryGetValue(card_id, out ActionCardView cardView))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActionCardsPlayed.Remove(card_id);
                    timestamps.Remove(card_id);
                    cardView.Count++;
                    timestamps.Add(card_id, DateTime.Now);
                    ActionCardsPlayed.Add(card_id, cardView);
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    timestamps.Add(card_id, DateTime.Now);
                    ActionCardsPlayed.Add(card_id, new ActionCardView(card_id));
                });
            }
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
