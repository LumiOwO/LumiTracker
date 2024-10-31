using LumiTracker.Config;
using LumiTracker.Models;
using System.Windows.Controls;
using LumiTracker.ViewModels.Pages;
using LumiTracker.Services;
using System.Windows.Data;
using LumiTracker.Helpers;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#pragma warning disable CS8618

namespace LumiTracker.ViewModels.Windows
{
    public partial class DeckWindowViewModel : ObservableObject
    {
        // ui
        [ObservableProperty]
        private double _mainContentHeightRatio = 0.0;

        [ObservableProperty]
        private bool _isShowing = false;

        [ObservableProperty]
        private bool _isChecked = true;

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

        private GameEventHook _hook;

        public DeckWindowViewModel(DeckViewModel deckViewModel, GameEventHook hook)
        {
            var binding = LocalizationExtension.Create("DeckWindowTitle");
            binding.Converter = new OverlayWindowTitleNameConverter();
            BindingOperations.SetBinding(DeckWindowTitle, LocalizationTextItem.TextProperty, binding);

            _deckViewModel = deckViewModel;
            _hook = hook;

            _hook.GameStarted        += OnGameStarted;
            _hook.MyActionCardPlayed += OnMyActionCardPlayed;
            _hook.OpActionCardPlayed += OnOpActionCardPlayed;
            _hook.GameOver           += OnGameOver;
            _hook.RoundDetected      += OnRoundDetected;
            _hook.MyCardsDrawn       += OnMyCardsDrawn;
            _hook.MyCardsCreateDeck  += OnMyCardsCreateDeck;
            _hook.OpCardsCreateDeck  += OnOpCardsCreateDeck;

            _hook.MyCharacters       += OnMyCharacters;
            _hook.OpCharacters       += OnOpCharacters;
            _hook.WindowWatcherExit  += OnWindowWatcherExit;

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
                MyDeck = new CardList(Enumerable.Repeat(-1, 30).ToArray(), inGame: true);
                DeckViewModel.UserDeckList.ActiveIndex = -1;
            }
            else
            {
                MyDeck = new CardList(inGame: true);
            }
        }

        public void InitDeckOnGameStart(string sharecode)
        {
            if (!MyDeck.InGame || MyDeck.OperationCount != 1)
            {
                return;
            }

            int[]? cards = DeckUtils.DecodeShareCode(sharecode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {sharecode}");
                return;
            }

            // Update the initial deck
            MyDeck.Remove(Enumerable.Repeat(-1, 30).ToArray(), keep_zero: false);
            MyDeck.Add(cards[3..]);

            // Try to message to server
            if (_hook is GameWatcher gameWatcher)
            {
                Task.Run(() => gameWatcher.SendMessageToServer(new GameEventMessage 
                { 
                    Event = EGameEvent.INITIAL_DECK,
                    Data  = new () { ["sharecode"] = JToken.FromObject(sharecode) },
                }));
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

        private void OnMyCharacters(int[] card_ids)
        {
            if (card_ids.Length != 3)
            {
                Configuration.Logger.LogError($"Number of my characters is not 3!");
                return;
            }

            var sortedDeckInfos = DeckViewModel.UserDeckList.DeckInfos
                .Select((info, index) => new { Info = info, Index = index })
                .OrderByDescending(x => x.Info.LastModified);

            bool found = false;
            foreach (var item in sortedDeckInfos)
            {
                var info = item.Info;
                if (info.Characters.Length == 3
                    && card_ids[0] == info.Characters[0]
                    && card_ids[1] == info.Characters[1]
                    && card_ids[2] == info.Characters[2] )
                {
                    InitDeckOnGameStart(info.ShareCode);
                    DeckViewModel.UserDeckList.ActiveIndex = item.Index;
                    DeckViewModel.SaveDeckList();
                    Configuration.Logger.LogInformation($"Set deck[{item.Index}] as active deck.");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Configuration.Logger.LogInformation($"Characters tuple ({card_ids[0]}, {card_ids[1]}, {card_ids[2]}) is not found in the deck list.");
            }
        }

        private void OnOpCharacters(int[] card_ids)
        {
            if (card_ids.Length != 6)
            {
                Configuration.Logger.LogError($"Number of (my + op) characters is not 6!");
                return;
            }
        }

        private void OnWindowWatcherExit()
        {
            Reset(gameStart: false);
        }

    }
}
