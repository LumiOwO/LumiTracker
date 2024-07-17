using System;
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
using Wpf.Ui.Appearance;

namespace LumiTracker.Models
{
    public class Deck
    {
        public Deck()
        {
            // TODO: remove test
            //LoadDeck("jl0ddrubjx09dxKblS0NgRWcls39gyScmV6tj0+dma7tl1OdmT4dmFadml6dm16dmm2N");
            LoadDeck("FdHAWTIVFZDh22EPFiAx8mMPE0Ax9TMPFFCB9kgPC2DR91gQDLEwDskQDOGQD80RDBAA");
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
