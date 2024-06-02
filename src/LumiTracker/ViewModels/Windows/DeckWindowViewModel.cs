using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.Views.Windows;
using LumiTracker.Helpers;

namespace LumiTracker.ViewModels.Windows
{
    public partial class DeckWindowViewModel : ObservableObject
    {
        private GameWatcher _gameWatcher;

        [ObservableProperty]
        private ObservableCollection<EventCardView> _basicListViewItems;

        [ObservableProperty]
        private bool _isShowing = false;

        private static ObservableCollection<EventCardView> GenerateEventCardViews()
        {
            var random = new Random();
            var cards = new ObservableCollection<EventCardView>();

            var names = new[]
            {
            "John",
            "Winston",
            "Adrianna",
            "交给我吧！",
            "Arielle",
            "Jamie",
            "Alexzander"
        };

            for (int i = 0; i < 50; i++)
            {
                cards.Add(
                    new EventCardView( )
                    {
                        CardID = i,
                        CardName = names[random.Next(0, names.Length)],
                        Count = random.Next(0, names.Length)
                    }
                );
            }

            return cards;
        }


        public DeckWindowViewModel(GameWatcher gameWatcher)
        {
            _gameWatcher = gameWatcher;
            _basicListViewItems = GenerateEventCardViews();
        }
    }
}
