using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using Swordfish.NET.Collections;

namespace LumiTracker.Models
{
    public partial class ActionCardView : ObservableObject
    {
        [ObservableProperty]
        private  int     _cost;
        [ObservableProperty]
        private  string  _costTypeUri;
        [ObservableProperty]
        private  string  _cardName;
        [ObservableProperty]
        private  string  _snapshotUri;
        [ObservableProperty]
        private  int     _count = 0;
        [ObservableProperty]
        private  double  _opacity;

        public ActionCardView(CardList? parent, int card_id, bool inGame, int count = 1)
        {
            var info = Configuration.Database["actions"]![card_id]!;
            var cardName = info[Configuration.Get<string>("lang")]!.ToString();
            var jCost    = info["cost"]!;
            var cost     = jCost[0]!.ToObject<int>();
            var costType = jCost[1]!.ToObject<ECostType>();

            // TODO: fix the temp fix for the tokens of Counting Down to 3 
            if (inGame && card_id == 301)
            {
                cost     = -1;
                cardName = "幻戏倒计时";
            }

            Cost         = cost;
            CostTypeUri  = $"pack://siteoforigin:,,,/assets/images/costs/{costType.ToString()}.png";
            CardName     = cardName;
            SnapshotUri  = $"pack://siteoforigin:,,,/assets/images/snapshots/{card_id}.jpg";
            Count        = count;
            Opacity      = 1.0;

            Parent       = parent;
        }

        private CardList? Parent;
        private int OperationIndex = 1;

        partial void OnCountChanged(int oldValue, int newValue)
        {
            Opacity = (Count == 0) ? 0.3 : 1.0;

            if (Parent != null) 
            {
                OperationIndex = Parent.OperationCount + 1;
            }
        }

        public bool ShouldNotify()
        {
            return OperationIndex == Parent?.OperationCount;
        }
    }

    public partial class CardList : ObservableObject
    {
        public enum SortType
        {
            Default,
            TimestampDescending
        }

        [ObservableProperty]
        public ConcurrentObservableSortedDictionary<int, ActionCardView> _data;

        public Dictionary<int, DateTime> Timestamps { get; private set; } = new ();

        public bool InGame { get; }

        public int OperationCount { get; private set; } = 0;

        public CardList(bool inGame, SortType sortType = SortType.Default)
        {
            IComparer<int> comparer;
            if (sortType == SortType.TimestampDescending)
            {
                comparer = Comparer<int>.Create((a, b) =>
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
                comparer = Comparer<int>.Create((a, b) => DeckUtils.ActionCardCompare(a, b));
            }

            Data = new ConcurrentObservableSortedDictionary<int, ActionCardView>(comparer);
            InGame = inGame;
        }

        public CardList(string shareCode, bool inGame, SortType sortType = SortType.Default) : this(inGame, sortType)
        {
            int[]? cards = DeckUtils.DecodeShareCode(shareCode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {shareCode}");
                return;
            }
            Add(cards[3..]);
        }

        public CardList(int[] card_ids, bool inGame, SortType sortType = SortType.Default) : this(inGame, sortType)
        {
            Add(card_ids);
        }

        public void Add(int card_id)
        {
            Add([card_id]);
        }

        public void Remove(int card_id, bool keep_zero)
        {
            Remove([card_id], keep_zero);
        }

        public void Add(int[] card_ids)
        {
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
            OperationCount++;

            Application.Current.Dispatcher.Invoke(() =>
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
            });
        }

        public void Remove(int[] card_ids, bool keep_zero)
        {
            var pairsToUpdate = new Dictionary<int, ActionCardView>();
            var keysToRemove  = new HashSet<int>();
            foreach (var card_id in card_ids)
            {
                ActionCardView cardView;
                if (pairsToUpdate.TryGetValue(card_id, out cardView!))
                {
                    if (cardView.Count == 0)
                    {
                        Configuration.Logger.LogWarning($"[CardList.Remove] Count of card id {card_id} is 0.");
                    }
                    else
                    {
                        cardView.Count -= 1;
                        if (cardView.Count == 0 && !keep_zero)
                        {
                            pairsToUpdate.Remove(card_id);
                        }
                    }
                }
                else if (Data.TryGetValue(card_id, out cardView!))
                {
                    if (cardView.Count == 0)
                    {
                        Configuration.Logger.LogWarning($"[CardList.Remove] Count of card id {card_id} is 0.");
                    }
                    else
                    {
                        cardView.Count -= 1;
                        if (cardView.Count == 0 && !keep_zero)
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
                }
            }
            OperationCount++;

            Application.Current.Dispatcher.Invoke(() =>
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
            });
        }
    }
}
