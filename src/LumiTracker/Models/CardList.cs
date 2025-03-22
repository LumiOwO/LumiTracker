using LumiTracker.Config;
using LumiTracker.Helpers;
using LumiTracker.Services;
using Microsoft.Extensions.Logging;
using Swordfish.NET.Collections;
using System.Windows.Data;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace LumiTracker.Models
{
    public partial class ActionCardView : ObservableObject
    {
        [ObservableProperty]
        private  int        _cardId;
        [ObservableProperty]
        private  ECostType  _costType;
        [ObservableProperty]
        private  int        _cost;
        [ObservableProperty]
        private  int        _count = 0;
        [ObservableProperty]
        private  double     _opacity = 1.0;
        [ObservableProperty]
        private  LocalizationTextItem  _cardName = new ();

        // For blink animation
        private CardList? Parent;
        private int OperationIndex { get; set; } = 1;
        private DateTime NotifiedTime { get; set; } = DateTime.Now;

        public ActionCardView(CardList? parent, int card_id, bool inGame, int count = 1)
        {
            Parent       = parent;

            ECostType costType = ECostType.Any;
            int cost = -1;
            if (card_id >= 0 && card_id < (int)EActionCard.NumActions)
            {
                var info  = Configuration.Database["actions"]![card_id]!;
                var jCost = info["cost"]!;
                cost      = jCost[0]!.ToObject<int>();
                costType  = jCost[1]!.ToObject<ECostType>();
            }

            CardId       = card_id;
            CostType     = costType;
            Cost         = cost;
            Count        = count;
            Opacity      = 1.0;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CardName = new ();
                var binding = LocalizationExtension.Create();
                binding.Converter = new ActionCardNameConverter();
                binding.ConverterParameter = card_id;
                BindingOperations.SetBinding(CardName, LocalizationTextItem.TextProperty, binding);
            });
        }

        partial void OnCountChanged(int oldValue, int newValue)
        {
            Opacity = (Count == 0) ? 0.3 : 1.0;

            if (Parent != null) 
            {
                OperationIndex = Parent.OperationCount + 1;
                NotifiedTime = DateTime.Now;
            }
        }

        public bool ShouldNotify()
        {
            return (OperationIndex == Parent?.OperationCount) && (DateTime.Now - NotifiedTime).TotalSeconds <= 1;
        }
    }

    public partial class CardList : ObservableObject
    {
        public enum ESortType
        {
            Default,
            TimestampDescending
        }

        [ObservableProperty]
        public ConcurrentObservableSortedDictionary<int, ActionCardView> _data = [];
        public Dictionary<int, DateTime> Timestamps { get; private set; } = [];

        public IList<KeyValuePair<int, ActionCardView>> View
        {
            get
            {
                return Data.CollectionView;
            }
        }

        private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs? e)
        {
            OnPropertyChanged(nameof(View));
        }

        public bool InGame { get; }

        public ESortType SortType { get; }

        public bool KeepZero { get; }

        public int OperationCount { get; private set; } = 0;

        private Comparer<int> Comparer { get; }

        [ObservableProperty]
        public int _count = 0;

        // For UI
        [ObservableProperty]
        private LocalizationTextItem _name = new ();

        [ObservableProperty]
        private ScrollBarVisibility _scrollBarVisibility = ScrollBarVisibility.Hidden;

        [ObservableProperty]
        private bool _isExpanded = false;

        public CardList(bool inGame = true, ESortType sortType = ESortType.Default, bool keepZero = false)
        {
            if (sortType == ESortType.TimestampDescending)
            {
                Comparer = Comparer<int>.Create((a, b) =>
                {
                    DateTime a_timestamp = Timestamps[a];
                    DateTime b_timestamp = Timestamps[b];
                    int res = a_timestamp.CompareTo(b_timestamp);
                    // Descending
                    return -res;
                });
            }
            else
            {
                // Default
                Comparer = Comparer<int>.Create((a, b) => DeckUtils.ActionCardCompare(a, b));
            }
            InGame   = inGame;
            SortType = sortType;
            KeepZero = keepZero;

            Reset();
        }

        public CardList SetName(string name, bool isBindingKey = true)
        {
            if (isBindingKey)
            {
                var binding = LocalizationExtension.Create(name);
                BindingOperations.SetBinding(Name, LocalizationTextItem.TextProperty, binding);
            }
            else
            {
                BindingOperations.ClearBinding(Name, LocalizationTextItem.TextProperty);
                Name.Text = "";
            }
            return this;
        }

        public CardList SetIsExpanded(bool value)
        {
            IsExpanded = value;
            return this;
        }

        public CardList Reset(int[]? card_ids = null)
        {
            Timestamps = [];
            Data = new ConcurrentObservableSortedDictionary<int, ActionCardView>(Comparer);
            Data.CollectionChanged += OnDataCollectionChanged;
            Count = 0;
            if (card_ids == null || card_ids.Length == 0)
            {
                // Notify ui to refresh
                OnDataCollectionChanged(null, null);
            }
            return Add(card_ids); //.Add(Enumerable.Range(0, 30).ToArray()); // Debug
        }

        public CardList Add(int card_id)
        {
            return Add([card_id]);
        }

        public CardList Remove(int card_id)
        {
            return Remove([card_id]);
        }

        public CardList Add(int[]? card_ids)
        {
            if (card_ids == null || card_ids.Length == 0) return this;

            var pairsToUpdate = new Dictionary<int, ActionCardView>();
            var keysToRemove  = new HashSet<int>();
            foreach (var card_id in card_ids)
            {
                ActionCardView cardView;
                if (pairsToUpdate.TryGetValue(card_id, out cardView!))
                {
                    cardView.Count += 1;
                }
                else if (Data.TryGetValue(card_id, out cardView!))
                {
                    cardView.Count += 1;
                    keysToRemove.Add(card_id);
                    pairsToUpdate.Add(card_id, cardView);
                }
                else
                {
                    pairsToUpdate.Add(card_id, new ActionCardView(this, card_id, InGame));
                }
            }
            Count += card_ids.Length;
            OperationCount++;

            var Update = () =>
            {
                Data.RemoveRange(keysToRemove);
                foreach (var key in keysToRemove)
                {
                    Timestamps.Remove(key);
                }

                var timestamp = DateTime.Now;
                foreach (var key in pairsToUpdate.Keys)
                {
                    Timestamps.Add(key, timestamp);
                }
                Data.AddRange(pairsToUpdate);
            };

            if (Application.Current == null)
            {
                Update();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Update();
                });
            }
            return this;
        }

        public CardList Remove(int[]? card_ids)
        {
            if (card_ids == null || card_ids.Length == 0) return this;

            var pairsToUpdate = new Dictionary<int, ActionCardView>();
            var keysToRemove  = new HashSet<int>();
            int invalidCount  = 0;
            foreach (var card_id in card_ids)
            {
                ActionCardView cardView;
                if (pairsToUpdate.TryGetValue(card_id, out cardView!))
                {
                    if (cardView.Count == 0)
                    {
                        Configuration.Logger.LogWarning($"[CardList.Remove] Count of card id {card_id} is 0.");
                        invalidCount++;
                    }
                    else
                    {
                        cardView.Count -= 1;
                        if (cardView.Count == 0 && !KeepZero)
                        {
                            pairsToUpdate.Remove(card_id);
                        }
                    }
                }
                else if (Data.TryGetValue(card_id, out cardView))
                {
                    if (cardView.Count == 0)
                    {
                        Configuration.Logger.LogWarning($"[CardList.Remove] Count of card id {card_id} is 0.");
                        invalidCount++;
                    }
                    else
                    {
                        cardView.Count -= 1;
                        if (cardView.Count == 0 && !KeepZero)
                        {
                            keysToRemove.Add(card_id);
                        }
                        else
                        {
                            keysToRemove.Add(card_id);
                            pairsToUpdate.Add(card_id, cardView);
                        }
                    }
                }
                else
                {
                    Configuration.Logger.LogWarning($"[CardList.Remove] Card id {card_id} is not in the deck.");
                    invalidCount++;
                }
            }
            if (invalidCount > 0 && Data.TryGetValue(-1, out ActionCardView unknownCardView))
            {
                int unknownCardCount = unknownCardView.Count;
                if (invalidCount > unknownCardCount)
                {
                    Configuration.Logger.LogWarning($"[CardList.Remove] Number of invalid cards ({invalidCount}) > number of unknown cards ({unknownCardCount})");
                    invalidCount -= unknownCardCount;
                    unknownCardCount = 0;
                }
                else
                {
                    unknownCardCount -= invalidCount;
                    invalidCount = 0;
                }
                unknownCardView.Count = unknownCardCount;

                keysToRemove.Add(-1);
                // Does not keep unknown cards if count == 0
                if (unknownCardCount > 0)
                {
                    pairsToUpdate.Add(-1, unknownCardView);
                }
            }
            Count -= card_ids.Length - invalidCount;
            OperationCount++;

            var Update = () =>
            {
                Data.RemoveRange(keysToRemove);
                foreach (var key in keysToRemove)
                {
                    Timestamps.Remove(key);
                }

                var timestamp = DateTime.Now;
                foreach (var key in pairsToUpdate.Keys)
                {
                    Timestamps.Add(key, timestamp);
                }
                Data.AddRange(pairsToUpdate);
            };

            if (Application.Current == null)
            {
                Update();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Update();
                });
            }
            return this;
        }
    }
}
