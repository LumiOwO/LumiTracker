using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace LumiTracker.Models
{
    public class DeckUtils
    {
        public static int[]? DecodeShareCode(string sharecode)
        {
            int[]? res = null;
            try
            {
                res = _DecodeShareCode(sharecode);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"An unexpected error occurred when decoding {sharecode}.\n{ex.ToString()}");
            }

            if (res != null)
            {
                Configuration.Logger.LogDebug($"Decoded from {sharecode}:\n[{string.Join(", ", res)}]");
            }
            return res;
        }

        private static int[]? _DecodeShareCode(string sharecode)
        {
            // Reference: https://gist.github.com/zyr17/36aae02c77d02602d6089f967027372a#file-deck_str_to_cards_4_2-py

            byte[]? code = null;
            code = Convert.FromBase64String(sharecode);
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
            int[] ids = new int[NUM_CARDS];
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
                ids[i] = shareId;
            }

            // 4. share id to internal id
            for (int i = 0; i < NUM_CARDS; i++)
            {
                int share_id = ids[i];
                if (share_id < 0 || share_id >= share_to_internal.Count())
                {
                    continue;
                }
                JToken? jId = share_to_internal[ids[i]];
                if (jId == null)
                {
                    continue;
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
                ids[i] = id;
            }
            //Configuration.Logger.LogDebug($"{string.Join(", ", internalIds)}");

            return ids;
        }

        public static int CharacterCompare(int a_character_id, int b_character_id, bool is_talent = false)
        {
            // Invalid id should be at the end
            bool a_invalid = (a_character_id < 0 || a_character_id >= (int)ECharacterCard.NumCharacters);
            bool b_invalid = (b_character_id < 0 || b_character_id >= (int)ECharacterCard.NumCharacters);
            if (a_invalid && b_invalid)
            {
                return a_character_id.CompareTo(b_character_id);
            }
            else if (a_invalid)
            {
                return 1;
            }
            else if (b_invalid)
            {
                return -1;
            }

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
                // 琳妮特天赋 和 散兵天赋，不知为何反了；琳妮特在前
                if (a_character_id == (int)ECharacterCard.Lynette && b_character_id == (int)ECharacterCard.Wanderer)
                {
                    return -1;
                }
                else if (a_character_id == (int)ECharacterCard.Wanderer && b_character_id == (int)ECharacterCard.Lynette)
                {
                    return 1;
                }
            }
            else
            {
                // 早柚 和 琳妮特，不知为何反了；早柚在前
                if (a_character_id == (int)ECharacterCard.Sayu && b_character_id == (int)ECharacterCard.Lynette)
                {
                    return -1;
                }
                else if (a_character_id == (int)ECharacterCard.Lynette && b_character_id == (int)ECharacterCard.Sayu)
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
            // Invalid id should be at the end
            bool a_invalid = (a_card_id < 0 || a_card_id >= (int)EActionCard.NumActions);
            bool b_invalid = (b_card_id < 0 || b_card_id >= (int)EActionCard.NumActions);
            if (a_invalid && b_invalid)
            {
                return a_card_id.CompareTo(b_card_id);
            }
            else if (a_invalid)
            {
                return 1;
            }
            else if (b_invalid)
            {
                return -1;
            }

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
            else if (a_type == EActionCardType.Event)
            {
                return EventCompare(a_card_id, b_card_id);
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

        public static int EventCompare(int a_card_id, int b_card_id)
        {
            float a = RemapEventTypeCardId(a_card_id);
            float b = RemapEventTypeCardId(b_card_id);
            return a.CompareTo(b);
        }

        private static float RemapEventTypeCardId(int id)
        {
            return (EActionCard)id switch 
            {
                EActionCard.CountdownToTheShow2 => (float)EActionCard.CountdownToTheShow3 + 0.1f,
                EActionCard.CountdownToTheShow1 => (float)EActionCard.CountdownToTheShow3 + 0.2f,
                EActionCard.TheShowBegins       => (float)EActionCard.CountdownToTheShow3 + 0.3f,
                _ => (float)id,
            };
        }

        public static void SortingTest()
        {
            // action cards
            {
                // Step 1: Generate the range of integers
                List<int> numbers = new List<int>();
                for (int i = 0; i < (int)EActionCard.NumSharables; i++)
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
                    output += $"{number}, {actions[number]!["zh-HANS"]!}\n";
                }
                Configuration.Logger.LogError(output);
            }

            // characters
            {
                // Step 1: Generate the range of integers
                List<int> numbers = new List<int>();
                for (int i = 0; i < (int)ECharacterCard.NumCharacters; i++)
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
                    output += $"{number}, {characters[number]!["zh-HANS"]!}\n";
                }
                Configuration.Logger.LogError(output);
            }
        }

        public static readonly string UnknownCharactersKey = "#Unknown";
        public static string CharacterIdsToKey(List<int> ids, bool ignoreOrder = true)
        {
            if (ids.Count != 3) return UnknownCharactersKey;
            foreach (var id in ids)
            {
                if (id < 0 || id >= (int)ECharacterCard.NumCharacters)
                {
                    return UnknownCharactersKey;
                }
            }

            if (ignoreOrder)
            {
                return "#" + string.Join(",", ids.OrderBy(x => x));
            }
            else
            {
                return "#" + string.Join(",", ids);
            }
        }

        public static string CharacterIdsToKey(int cid0, int cid1, int cid2, bool ignoreOrder = true)
        {
            return CharacterIdsToKey([cid0, cid1, cid2], ignoreOrder);
        }

        public static Guid DeckBuildGuid(int[]? cards, bool should_sort = true)
        {
            if (cards == null || cards.Length != 33) return Guid.Empty;

            if (should_sort)
            {
                Array.Sort(cards, 3, cards.Length - 3);
            }

            byte[] byteArray = cards.SelectMany(BitConverter.GetBytes).ToArray();
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(byteArray);
                return new Guid(hash);
            }
        }

        public static string GetActualDeckName(BuildStats stats)
        {
            string? name = stats.Edit.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "";
                foreach (int c_id in stats.CharacterIds)
                {
                    if (name != "") name += "+";
                    name += Configuration.GetCharacterName(c_id, is_short: true);
                }
            }
            return name;
        }
    }
}
