using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Swordfish.NET.Collections;
using System.Collections.ObjectModel;

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
        private  int     _count;
        [ObservableProperty]
        private  double  _opacity;

        public ActionCardView(int card_id, int count = 1)
        {
            var info = Configuration.Database["actions"]![card_id]!;
            var cardName = info[Configuration.Data.lang]!.ToString();
            var jCost    = info["cost"]!;
            var cost     = jCost[0]!.ToObject<int>();
            var costType = jCost[1]!.ToObject<ECostType>();

            Cost         = cost;
            CostTypeUri  = $"pack://siteoforigin:,,,/assets/images/costs/{costType.ToString()}.png";
            CardName     = cardName;
            SnapshotUri  = $"pack://siteoforigin:,,,/assets/images/snapshots/{card_id}.jpg";
            Count        = count;
            Opacity      = 1.0;
        }

        partial void OnCountChanged(int oldValue, int newValue)
        {
            Opacity = (Count == 0) ? 0.3 : 1.0;
        }
    }

    public partial class AvatarView : ObservableObject
    {
        [ObservableProperty]
        public string _avatarUri;

        public AvatarView(int character_id)
        {
            var info = Configuration.Database["characters"]![character_id]!;
            AvatarUri = $"pack://siteoforigin:,,,/assets/images/avatars/{character_id}.jpg";
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

        public CardList() : this(SortType.Default) { }

        public CardList(SortType sortType)
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
        }

        public CardList(string shareCode, SortType sortType = SortType.Default) : this(sortType)
        {
            int[]? cards = DeckUtils.DecodeShareCode(shareCode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {shareCode}");
                return;
            }
            Add(cards[3..]);
        }

        public CardList(int[] card_ids, SortType sortType = SortType.Default) : this(sortType)
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
                    pairsToUpdate.Add(card_id, new ActionCardView(card_id));
                }
            }

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

    public partial class DeckModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<AvatarView> _avatars = new();

        [ObservableProperty]
        private CardList _deck = new CardList();

        public DeckModel() { }

        public DeckModel(string shareCode)
        {
            int[]? cards = DeckUtils.DecodeShareCode(shareCode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {shareCode}");
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                Avatars.Add(new AvatarView(cards[i]));
            }
            Deck.Add(cards[3..]);
        }
    }
}
