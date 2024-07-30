using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Models;
using System.Collections.ObjectModel;
using Swordfish.NET.Collections.Auxiliary;

namespace LumiTracker.ViewModels.Pages
{
    public partial class AvatarView : ObservableObject
    {
        [ObservableProperty]
        public string _avatarUri = "pack://siteoforigin:,,,/assets/images/avatars/0.png";

        [ObservableProperty]
        public Visibility _avatarImageVisibility = Visibility.Hidden;

        [ObservableProperty]
        public Visibility _placeholderVisibility = Visibility.Visible;
        partial void OnAvatarImageVisibilityChanged(Visibility oldValue, Visibility newValue)
        {
            if (newValue == Visibility.Collapsed || newValue == Visibility.Hidden)
            {
                PlaceholderVisibility = Visibility.Visible;
            }
            else
            {
                PlaceholderVisibility = Visibility.Collapsed;
            }
        }

        public AvatarView() { }

        public AvatarView(int character_id)
        {
            var info = Configuration.Database["characters"]![character_id]!;
            AvatarUri = $"pack://siteoforigin:,,,/assets/images/avatars/{character_id}.png";
            AvatarImageVisibility = Visibility.Visible;
        }
    }


    public partial class DeckViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private ObservableCollection<AvatarView> _avatars = new () { new AvatarView(), new AvatarView(), new AvatarView() };

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _currentDeck = new ();

        public DeckViewModel()
        {
            string shareCode = Configuration.Get<string>("share_code");
            int[]? cards = DeckUtils.DecodeShareCode(shareCode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {shareCode}");
                return;
            }

            ///////////////////////
            // Avatars
            for (int i = 0; i < 3; i++)
            {
                Avatars[i] = new AvatarView(cards[i]);
            }

            ///////////////////////
            // Current Deck

            // Sort in case of non-standard order
            Array.Sort(cards, 3, 30, 
                Comparer<int>.Create((x, y) => DeckUtils.ActionCardCompare(x, y)));

            // To column major
            int[] transposed = new int[30];
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    transposed[y * 2 + x] = cards[3 + x * 15 + y];
                }
            }

            foreach (int card_id in transposed)
            {
                CurrentDeck.Add(new ActionCardView(card_id));
            }
        }

        public void OnNavigatedTo()
        {
            
        }

        public void OnNavigatedFrom() 
        {

        }
    }
}
