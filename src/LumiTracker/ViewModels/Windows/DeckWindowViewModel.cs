using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;


namespace LumiTracker.ViewModels.Windows
{
    public partial class ActionCardView : ObservableObject
    {
        [ObservableProperty]
        public int _count;
        [ObservableProperty]
        public int _cardID;
        [ObservableProperty]
        public string _cardName;
        [ObservableProperty]
        public string _snapshotUri;

        public ActionCardView(int card_id) 
        {
            var cardInfo  = Configuration.Database["actions"]![card_id]!;
            _cardID       = card_id;
            _cardName     = cardInfo["zh-HANS"]!.ToString(); // TODO: localization
            _count        = 1;
            _snapshotUri  = $"pack://siteoforigin:,,,/assets/snapshots/actions/{card_id}.jpg";
        }
    }

    public partial class DeckWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ActionCardView> _myActionCardsPlayed = new ();
        HashSet<int> MyPlayedCardIDs = new ();

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _opActionCardsPlayed = new ();
        HashSet<int> OpPlayedCardIDs = new ();

        [ObservableProperty]
        private bool _gameStarted = false;

        [ObservableProperty]
        private bool _gameNotStarted = true;

        [ObservableProperty]
        private int _round = 0;

        [ObservableProperty]
        private bool _isShowing = false;

        [ObservableProperty]
        private ScrollBarVisibility _VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

        [ObservableProperty]
        private SymbolRegular _toggleButtonIcon = SymbolRegular.ChevronUp48;

        private GameWatcher _gameWatcher;
        // TODO: remove deck test
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
        }

        private void ResetRecordedData()
        {
            MyActionCardsPlayed = new();
            MyPlayedCardIDs     = new();
            OpActionCardsPlayed = new();
            OpPlayedCardIDs     = new();

            Round = 0;
        }

        private void UpdatePlayedActionCard(
            int card_id, 
            ObservableCollection<ActionCardView> ActionCardsPlayed, 
            HashSet<int> PlayedCardIDs)
        {
            if (PlayedCardIDs.Contains(card_id))
            {
                int found_idx = -1;
                for (int i = 0; i < ActionCardsPlayed.Count; i++)
                {
                    if (ActionCardsPlayed[i].CardID == card_id)
                    {
                        ActionCardsPlayed[i].Count++;
                        found_idx = i;
                        break;
                    }
                }

                if (found_idx != 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ActionCardsPlayed.Move(found_idx, 0);
                    });
                }
            }
            else
            {
                PlayedCardIDs.Add(card_id);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActionCardsPlayed.Insert(0, new ActionCardView(card_id));
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
            UpdatePlayedActionCard(card_id, MyActionCardsPlayed, MyPlayedCardIDs);
        }

        private void OnOpActionCardPlayed(int card_id)
        {
            UpdatePlayedActionCard(card_id, OpActionCardsPlayed, OpPlayedCardIDs);
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
