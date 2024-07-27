﻿using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Wpf.Ui.Appearance;

namespace LumiTracker.Models
{
    public class Deck
    {
        public Deck()
        {
            // TODO: remove test
            LoadDeck("GLGxC4wQGMHRDI4QGNHxDZAQGeERDpIQGfExD5QRGQFREJYREBGREQkRECGhEgoREDAA");

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

        public void LoadDeck(string shareCode)
        {
            // Reference: https://gist.github.com/zyr17/36aae02c77d02602d6089f967027372a#file-deck_str_to_cards_4_2-py

            // TODO: error check
            byte[] code = Convert.FromBase64String(shareCode);
            // TODO: assert (code.Length == 51)
            const int LENGTH = 50;

            // 1. subtract the obfuscation code
            byte key = code[LENGTH];
            for (int i = 0; i < LENGTH; i++)
            {
                code[i] -= key;
            }

            // 2. swap the bytes
            byte[] swapped = new byte[LENGTH];
            for (int i = 0; i < LENGTH; i++)
            {
                int idx = i / 2 + ( ((i & 0x1) != 0) ? (LENGTH / 2) : 0 );
                swapped[idx] = code[i];
            }

            // 3. decode by 12-bits stride
            int bitIndex = 0;
            const int NUM_CARDS = 33;
            int[] result = new int[NUM_CARDS];
            for (int i = 0; i < NUM_CARDS; i++)
            {
                int byteIndex = bitIndex >> 3;  // bitIndex / 8;
                int bitOffset = bitIndex & 0x7; // bitIndex % 8;

                int value = 0;
                if (bitOffset == 0)
                {
                    value = (swapped[byteIndex] << 4 | swapped[byteIndex + 1] >> 4) & 0xFFF;
                }
                else
                {
                    value = ((swapped[byteIndex] & 0x0F) << 8 | swapped[byteIndex + 1]) & 0xFFF;
                }

                result[i] = value;
                bitIndex += 12;
            }

            Configuration.Logger.LogDebug($"{string.Join(", ", result)}");
        }

    }
}
