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
            MyActionCardsPlayed = new CardList(sortType: CardList.ESortType.TimestampDescending);
            OpActionCardsPlayed = new CardList(sortType: CardList.ESortType.TimestampDescending);
            TrackedCards = new CardList(sortType: CardList.ESortType.TimestampDescending);
            Round = 0;
            GameStarted = gameStart;
            if (gameStart)
            {
                MyDeck = new CardList(Enumerable.Repeat(-1, 30).ToArray());
                DeckViewModel.ActiveDeckIndex = -1;
            }
            else
            {
                MyDeck = new CardList();
            }
        }

        public void InitDeckOnGameStart(string sharecode, int[] action_cards)
        {
            if (!MyDeck.InGame || !MyDeck.Data.Keys.All(x => x == -1))
            {
                return;
            }

            // Update the initial deck
            MyDeck = new CardList(action_cards, inGame: true);

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

        private void OnGameOver(Dictionary<string, JToken> record)
        {
            try
            {
                var duelRecord = JObject.FromObject(record).ToObject<DuelRecord>();
                if (duelRecord == null)
                {
                    throw new Exception("[OnGameOver] Failed to parse DuelRecord.");
                }

                DeckViewModel.AddRecordToActiveDeck(duelRecord);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"JSON deserialization Error: {ex.Message}");
            }

            Reset(gameStart: false);
        }

        private void OnRoundDetected(int round)
        {
            Round = round;
        }

        private void OnMyCardsDrawn(int[] card_ids)
        {
            MyDeck.Remove(card_ids);
            var tracked = card_ids.Where(x => CardsToTrack.Contains((EActionCard)x)).ToArray();
            if (tracked != null)
            {
                TrackedCards.Remove(tracked);
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

            bool found = FindMatchedDeckBuildAndSetActive(card_ids);
            if (!found)
            {
                Configuration.Logger.LogInformation($"Characters tuple ({card_ids[0]}, {card_ids[1]}, {card_ids[2]}) is not found in the deck list.");
            }
        }

        private bool FindMatchedDeckBuildAndSetActive(int[] cids)
        {
            var sortedDeckItemWrappers = DeckViewModel.DeckItems
                .Select((item, index) => new
                {
                    Index = index,
                    Item  = item,
                    LastModified = item.Info.LastModified,
                })
                .OrderByDescending(x => x.LastModified);

            string sortedKey = DeckUtils.CharacterIdsToKey(cids[0], cids[1], cids[2], ignoreOrder: true);
            if (sortedKey == DeckUtils.UnknownCharactersKey)
                return false;

            string originKey = DeckUtils.CharacterIdsToKey(cids[0], cids[1], cids[2], ignoreOrder: false);
            foreach (var data in sortedDeckItemWrappers)
            {
                var item = data.Item;
                int matchedIndex = item.Stats.FindMatchedBuildVersionIndex(sortedKey, originKey);
                if (matchedIndex != -1)
                {
                    Configuration.Logger.LogInformation($"Set deck[{data.Index}] as active deck.");
                    // UI related
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DeckViewModel.ActiveDeckIndex = data.Index;
                        if (matchedIndex != (item.Info.CurrentVersionIndex ?? 0))
                        {
                            item.Info.CurrentVersionIndex = matchedIndex;
                        }
                    });

                    var build = DeckViewModel.DeckItems[data.Index].Stats.AllBuildStats[matchedIndex];
                    InitDeckOnGameStart(build.Edit.ShareCode, build.Cards[3..]);

                    return true;
                }
            }

            return false;
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
