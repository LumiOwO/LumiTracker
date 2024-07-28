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

        public CardList(SortType sortType = SortType.Default)
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
                comparer = Comparer<int>.Create((a, b) => DeckModel.ActionCardCompare(a, b));
            }

            Data = new ConcurrentObservableSortedDictionary<int, ActionCardView>(comparer);
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
            int[]? cards = DecodeShareCode(shareCode);
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

        public static int[]? DecodeShareCode(string shareCode)
        {
            // Reference: https://gist.github.com/zyr17/36aae02c77d02602d6089f967027372a#file-deck_str_to_cards_4_2-py

            byte[]? code = null;
            try
            {
                code = Convert.FromBase64String(shareCode);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"An unexpected error occurred when decoding {shareCode}.\n{ex.ToString()}");
                return null;
            }
            if (code.Length != 51)
            {
                return null;
            }

            // 1. subtract the obfuscation code
            const int LENGTH = 50;
            byte key = code[LENGTH];
            for (int i = 0; i < LENGTH; i++)
            {
                code[i] -= key;
            }

            // 2. swap the bytes
            byte[] swapped = new byte[LENGTH];
            for (int i = 0; i < LENGTH; i++)
            {
                int idx = i / 2 + (((i & 0x1) != 0) ? (LENGTH / 2) : 0);
                swapped[idx] = code[i];
            }

            // 3. decode by 12-bits stride
            const int NUM_CARDS = 33;
            int[] internalIds = new int[NUM_CARDS];
            var share_to_internal = Configuration.Database["share_to_internal"]!;

            int bitIndex = 0;
            for (int i = 0; i < NUM_CARDS; i++, bitIndex += 12)
            {
                int byteIndex = bitIndex >> 3;  // bitIndex / 8;
                int bitOffset = bitIndex & 0x7; // bitIndex % 8;
                // Cast to int, in case of uint8 overflow
                int curByte = swapped[byteIndex];
                int nextByte = swapped[byteIndex + 1];

                int shareId = 0;
                if (bitOffset == 0)
                {
                    shareId = (curByte << 4 | nextByte >> 4) & 0xfff;
                }
                else
                {
                    shareId = ((curByte & 0x0f) << 8 | nextByte) & 0xfff;
                }

                // share id to internal id
                JToken? jId = share_to_internal[shareId];
                if (jId == null)
                {
                    return null;
                }
                int id = jId.ToObject<int>();
                if (id < 0)
                {
                    id = (-id) - 1;
                }
                else
                {
                    id = id - 1;
                }
                internalIds[i] = id;
            }
            //Configuration.Logger.LogDebug($"{string.Join(", ", internalIds)}");

            return internalIds;
        }

        public static int CharacterCompare(int a_character_id, int b_character_id, bool is_talent = false)
        {
            var characters = Configuration.Database["characters"]!;
            var a_info = characters[a_character_id]!;
            var b_info = characters[b_character_id]!;

            /////////////////////////
            // is_monster
            bool a_is_monster = a_info["is_monster"]!.ToObject<bool>();
            bool b_is_monster = b_info["is_monster"]!.ToObject<bool>();
            if (a_is_monster != b_is_monster)
            {
                return a_is_monster.CompareTo(b_is_monster);
            }

            /////////////////////////
            // element
            EElementType a_element = a_info["element"]!.ToObject<EElementType>();
            EElementType b_element = b_info["element"]!.ToObject<EElementType>();
            if (a_element != b_element)
            {
                return a_element.CompareTo(b_element);
            }

            /////////////////////////
            // special cases
            if (is_talent)
            {
                // 琳妮特(65)天赋 和 散兵(40)天赋，不知为何反了；琳妮特在前
                if (a_character_id == 65 && b_character_id == 40)
                {
                    return -1;
                }
                else if (a_character_id == 40 && b_character_id == 65)
                {
                    return 1;
                }
            }
            else
            {
                // 早柚(72) 和 琳妮特(65)，不知为何反了；早柚在前
                if (a_character_id == 72 && b_character_id == 65)
                {
                    return -1;
                }
                else if (a_character_id == 65 && b_character_id == 72)
                {
                    return 1;
                }
            }

            /////////////////////////
            // id
            return a_character_id.CompareTo(b_character_id);
        }

        public static int ActionCardCompare(int a_card_id, int b_card_id)
        {
            var actions = Configuration.Database["actions"]!;
            var a_info = actions[a_card_id]!;
            var b_info = actions[b_card_id]!;

            /////////////////////////
            // type
            EActionCardType a_type = a_info["type"]!.ToObject<EActionCardType>();
            EActionCardType b_type = b_info["type"]!.ToObject<EActionCardType>();
            if (a_type != b_type)
            {
                return a_type.CompareTo(b_type);
            }

            /////////////////////////
            // id
            if (a_type == EActionCardType.Talent)
            {
                return TalentCompare(a_card_id, b_card_id);
            }
            else if (a_type == EActionCardType.Artifact)
            {
                return ArtifactCompare(a_card_id, b_card_id);
            }
            else
            {
                return a_card_id.CompareTo(b_card_id);
            }
        }

        public static int TalentCompare(int a_card_id, int b_card_id)
        {
            var talent_to_character = Configuration.Database["talent_to_character"]!;
            int a_character_id = talent_to_character[$"{a_card_id}"]!.ToObject<int>();
            int b_character_id = talent_to_character[$"{b_card_id}"]!.ToObject<int>();
            return CharacterCompare(a_character_id, b_character_id, is_talent: true);
        }

        public static int ArtifactCompare(int a_card_id, int b_card_id)
        {
            var artifacts_order = Configuration.Database["artifacts_order"]!;
            int a = artifacts_order[$"{a_card_id}"]!.ToObject<int>();
            int b = artifacts_order[$"{b_card_id}"]!.ToObject<int>();
            return a.CompareTo(b);
        }

        private static void SortingTest()
        {
            // action cards
            {
                // Step 1: Generate the range of integers
                List<int> numbers = new List<int>();
                for (int i = 0; i <= 310; i++)
                {
                    numbers.Add(i);
                }

                // Step 2: Shuffle the integers randomly
                Random rng = new Random();
                int n = numbers.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    int value = numbers[k];
                    numbers[k] = numbers[n];
                    numbers[n] = value;
                }

                // Step 3: Sort the shuffled integers with a custom comparison function
                numbers.Sort((a, b) => ActionCardCompare(a, b));

                // Output the sorted list
                string output = "";
                var actions = Configuration.Database["actions"]!;
                foreach (var number in numbers)
                {
                    JArray costs = (actions[number]!["costs"]! as JArray)!;
                    string costVals = "[";
                    foreach (var cost in costs)
                    {
                        costVals += $"{cost[0]!.ToObject<int>()}, ";
                    }
                    costVals += "]";
                    string element = costs[0]![1]!.ToObject<ECostType>().ToString();
                    output += $"{actions[number]!["type"]!}, {actions[number]!["zh-HANS"]!}, {element}, {costVals}\n";
                }
                Configuration.Logger.LogDebug(output);
            }

            // characters
            {
                // Step 1: Generate the range of integers
                List<int> numbers = new List<int>();
                for (int i = 0; i <= 94; i++)
                {
                    numbers.Add(i);
                }

                // Step 2: Shuffle the integers randomly
                Random rng = new Random();
                int n = numbers.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    int value = numbers[k];
                    numbers[k] = numbers[n];
                    numbers[n] = value;
                }

                // Step 3: Sort the shuffled integers with a custom comparison function
                numbers.Sort((a, b) => CharacterCompare(a, b));

                // Output the sorted list
                string output = "";
                var characters = Configuration.Database["characters"]!;
                foreach (var number in numbers)
                {
                    output += $"{characters[number]!["zh-HANS"]!}\n";
                }
                Configuration.Logger.LogDebug(output);
            }
        }

    }
}
