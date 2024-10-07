using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;
using LumiTracker.ViewModels.Pages;
using LumiTracker.Services;
using System.Windows.Data;
using LumiTracker.Helpers;

#pragma warning disable CS8618

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

        [ObservableProperty]
        private LocalizationTextItem _deckWindowTitle = new ();

        // data
        [ObservableProperty]
        private CardList _myActionCardsPlayed;

        [ObservableProperty]
        private CardList _opActionCardsPlayed;

        [ObservableProperty]
        private CardList _myDeck;

        [ObservableProperty]
        private CardList _trackedCards;

        static private readonly HashSet<EActionCard> CardsToTrack = [
            EActionCard.CalledInForCleanup,
            EActionCard.UnderseaTreasure,
            EActionCard.AwakenMyKindred,
            EActionCard.ForbiddenKnowledge,
            EActionCard.BonecrunchersEnergyBlock,
            EActionCard.TaroumarusSavings,
        ];

        public string ShareCodeOverride { get; set; } = "";

        // controls
        [ObservableProperty]
        private bool _gameStarted = false;

        [ObservableProperty]
        private bool _gameNotStarted = true;
        partial void OnGameStartedChanged(bool oldValue, bool newValue)
        {
            GameNotStarted = !GameStarted;
        }

        [ObservableProperty]
        private int _round = 0;

        [ObservableProperty]
        private DeckViewModel _deckViewModel;

        private GameWatcher _gameWatcher;

        public DeckWindowViewModel(DeckViewModel deckViewModel, GameWatcher gameWatcher)
        {
            var binding = LocalizationExtension.Create("DeckWindowTitle");
            binding.Converter = new OverlayWindowTitleNameConverter();
            BindingOperations.SetBinding(DeckWindowTitle, LocalizationTextItem.TextProperty, binding);

            _deckViewModel = deckViewModel;
            _gameWatcher   = gameWatcher;

            _gameWatcher.GameStarted        += OnGameStarted;
            _gameWatcher.MyActionCardPlayed += OnMyActionCardPlayed;
            _gameWatcher.OpActionCardPlayed += OnOpActionCardPlayed;
            _gameWatcher.GameOver           += OnGameOver;
            _gameWatcher.RoundDetected      += OnRoundDetected;
            _gameWatcher.MyCardsDrawn       += OnMyCardsDrawn;
            _gameWatcher.MyCardsCreateDeck  += OnMyCardsCreateDeck;
            _gameWatcher.OpCardsCreateDeck  += OnOpCardsCreateDeck;
            _gameWatcher.UnsupportedRatio   += OnUnsupportedRatio;

            _gameWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            Reset(gameStart: false);
        }

        private void Reset(bool gameStart)
        {
            MyActionCardsPlayed = new CardList(inGame: true, sortType: CardList.SortType.TimestampDescending);
            OpActionCardsPlayed = new CardList(inGame: true, sortType: CardList.SortType.TimestampDescending);
            TrackedCards = new CardList(inGame: true, sortType: CardList.SortType.TimestampDescending);
            Round = 0;
            GameStarted = gameStart;
            if (gameStart)
            {
                string sharecode = ShareCodeOverride;
                int activeIndex  = DeckViewModel.UserDeckList.ActiveIndex;
                if (sharecode == "" && activeIndex >= 0)
                {
                    sharecode = DeckViewModel.UserDeckList.DeckInfos[activeIndex].ShareCode;
                }

                if (sharecode == "")
                {
                    MyDeck = new CardList(inGame: true);
                }
                else
                {
                    MyDeck = new CardList(sharecode, inGame: true);
                }
            }
            else
            {
                MyDeck = new CardList(inGame: true);
            }
        }

        private void UpdatePlayedActionCard(int card_id, bool is_op)
        {
            CardList ActionCardsPlayed = is_op ? OpActionCardsPlayed : MyActionCardsPlayed;
            ActionCardsPlayed.Add(card_id);
        }

        private void OnGameStarted()
        {
            Reset(gameStart: true);
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
            Reset(gameStart: false);
        }

        private void OnRoundDetected(int round)
        {
            Round = round;
        }

        private void OnMyCardsDrawn(int[] card_ids)
        {
            MyDeck.Remove(card_ids, keep_zero: true);
            var tracked = card_ids.Where(x => CardsToTrack.Contains((EActionCard)x)).ToArray();
            if (tracked != null)
            {
                TrackedCards.Remove(tracked, keep_zero: false);
            }
        }

        private void OnMyCardsCreateDeck(int[] card_ids)
        {
            MyDeck.Add(card_ids);
            var tracked = card_ids.Where(x => CardsToTrack.Contains((EActionCard)x)).ToArray();
            if (tracked != null)
            {
                TrackedCards.Add(card_ids);
            }
        }

        private void OnOpCardsCreateDeck(int[] card_ids)
        {

        }

        private void OnUnsupportedRatio()
        {
            EClientType clientType = _gameWatcher.ClientType;
            // Ignore MessageBox popup when client is web browser
            if (clientType != EClientType.CloudWeb && clientType != EClientType.WeMeet)
            {
                System.Windows.MessageBox.Show(
                    $"{LocalizationSource.Instance["UnsupportedRatioWarning"]}\n{LocalizationSource.Instance["SupportedRatioInfo"]}", 
                    $"{LocalizationSource.Instance["AppName"]}", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void OnWindowWatcherExit()
        {
            Reset(gameStart: false);
        }

    }
}
