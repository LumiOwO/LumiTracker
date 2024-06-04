using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.Views.Windows;
using LumiTracker.Helpers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.ComponentModel;

namespace LumiTracker.ViewModels.Windows
{
    public partial class EventCardView : ObservableObject
    {
        [ObservableProperty]
        public int _count;
        [ObservableProperty]
        public int _cardID;
        [ObservableProperty]
        public string _cardName;
        [ObservableProperty]
        public string _snapshotUri;

        public EventCardView(int card_id) 
        {
            var cardInfo  = Configuration.Database["events"]![card_id]!;
            _cardID       = card_id;
            _cardName     = cardInfo["zh-HANS"]!.ToString(); // TODO: localization
            _count        = 1;
            _snapshotUri  = $"pack://siteoforigin:,,,/assets/snapshots/events/{card_id}.jpg";
        }
    }

    public partial class DeckWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<EventCardView> _myEventCardsPlayed = new ();
        HashSet<int> MyPlayedCardIDs = new ();

        [ObservableProperty]
        private ObservableCollection<EventCardView> _opEventCardsPlayed = new ();
        HashSet<int> OpPlayedCardIDs = new ();

        [ObservableProperty]
        private bool _isShowing = false;

        [ObservableProperty]
        private ScrollBarVisibility _VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

        private GameWatcher _gameWatcher;
        public DeckWindowViewModel(GameWatcher gameWatcher)
        {
            _gameWatcher = gameWatcher;
            _gameWatcher.GameStarted += OnGameStarted;
            _gameWatcher.MyEventCard += OnMyEventCard;
            _gameWatcher.OpEventCard += OnOpEventCard;
        }

        private void OnGameStarted()
        {
            MyEventCardsPlayed = new ();
            MyPlayedCardIDs    = new ();
            OpEventCardsPlayed = new ();
            OpPlayedCardIDs    = new ();
        }

        private void UpdatePlayedEventCard(
            int card_id, 
            ObservableCollection<EventCardView> EventCardsPlayed, 
            HashSet<int> PlayedCardIDs)
        {
            if (PlayedCardIDs.Contains(card_id))
            {
                int found_idx = -1;
                for (int i = 0; i < EventCardsPlayed.Count; i++)
                {
                    if (EventCardsPlayed[i].CardID == card_id)
                    {
                        EventCardsPlayed[i].Count++;
                        found_idx = i;
                        break;
                    }
                }

                if (found_idx != 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        EventCardsPlayed.Move(found_idx, 0);
                    });
                }
            }
            else
            {
                PlayedCardIDs.Add(card_id);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    EventCardsPlayed.Insert(0, new EventCardView(card_id));
                });
            }
        }

        private void OnMyEventCard(int card_id)
        {
            UpdatePlayedEventCard(card_id, MyEventCardsPlayed, MyPlayedCardIDs);
        }

        private void OnOpEventCard(int card_id)
        {
            UpdatePlayedEventCard(card_id, OpEventCardsPlayed, OpPlayedCardIDs);
        }
    }
}
