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
        private ObservableCollection<Person> _basicListViewItems;

        [ObservableProperty]
        private bool _isShowing = false;

        private static ObservableCollection<Person> GeneratePersons()
        {
            var random = new Random();
            var persons = new ObservableCollection<Person>();

            var names = new[]
            {
            "John",
            "Winston",
            "Adrianna",
            "Spencer",
            "Phoebe",
            "Lucas",
            "Carl",
            "Marissa",
            "Brandon",
            "Antoine",
            "Arielle",
            "Arielle",
            "Jamie",
            "Alexzander"
        };
            var surnames = new[]
            {
            "Doe",
            "Tapia",
            "Cisneros",
            "Lynch",
            "Munoz",
            "Marsh",
            "Hudson",
            "Bartlett",
            "Gregory",
            "Banks",
            "Hood",
            "Fry",
            "Carroll"
        };
            var companies = new[]
            {
            "Pineapple Inc.",
            "Macrosoft Redmond",
            "Amazing Basics Ltd",
            "Megabyte Computers Inc",
            "Roude Mics",
            "XD Projekt Red S.A.",
            "Lepo.co"
        };

            for (int i = 0; i < 50; i++)
            {
                persons.Add(
                    new Person(
                        names[random.Next(0, names.Length)],
                        surnames[random.Next(0, surnames.Length)],
                        companies[random.Next(0, companies.Length)]
                    )
                );
            }

            return persons;
        }


        public DeckWindowViewModel(GameWatcher gameWatcher)
        {
            _gameWatcher = gameWatcher;
            _basicListViewItems = GeneratePersons();
        }
    }
}
